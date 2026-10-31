using System.Text;
using System.Text.Json;
using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Chats.Tools;
using BookMyDoctor.Features.Knowledge;
using BookMyDoctor.Features.Settings;
using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Chats;

public sealed class OllamaChatAssistant : IChatAssistant
{
    // Sent when the model keeps claiming a booking that didn't happen.
    internal const string NotBookedReply =
        "Sorry, your appointment is not booked yet. Please pick a time from the buttons and confirm it, or ask me for free times again.";

    private readonly HttpClient _http;
    private readonly IAiSettingsProvider _settings;
    private readonly IChatService _chatService;
    private readonly IUsersRepo _usersRepo;
    private readonly IHospitalRepo _hospitalRepo;
    private readonly IKnowledgeRetriever _retriever;
    private readonly IReadOnlyDictionary<string, IAssistantTool> _tools;
    private readonly ToolTranscripts _transcripts;
    private readonly ReplyFactChecker _factChecker;
    private readonly TimeProvider _time;
    private readonly ILogger<OllamaChatAssistant> _logger;

    public OllamaChatAssistant(
        HttpClient http,
        IAiSettingsProvider settings,
        IChatService chatService,
        IUsersRepo usersRepo,
        IHospitalRepo hospitalRepo,
        IKnowledgeRetriever retriever,
        IEnumerable<IAssistantTool> tools,
        ToolTranscripts transcripts,
        ReplyFactChecker factChecker,
        TimeProvider time,
        ILogger<OllamaChatAssistant> logger)
    {
        _http = http;
        _settings = settings;
        _chatService = chatService;
        _usersRepo = usersRepo;
        _hospitalRepo = hospitalRepo;
        _retriever = retriever;
        _tools = tools.ToDictionary(tool => tool.Name);
        _transcripts = transcripts;
        _factChecker = factChecker;
        _time = time;
        _logger = logger;
    }

    public async Task<AssistantReply> ReplyAsync(ChatMessage patientMessage, IReplyDraft? draft = null)
    {
        // Read once per message, so a change on the Settings page applies from the next message.
        var settings = _settings.Get();
        var name = _usersRepo.GetById(patientMessage.PatientId)?.Name ?? "there";
        // The patient's new message was already saved by the hub, so it's the last one here.
        var history = RecentHistory(_chatService.GetMessages(patientMessage.PatientId), settings.MaxHistoryMessages);

        // 1. Retrieve: find the knowledge chunks most related to what the patient just asked.
        var knowledge = await RetrieveAsync(patientMessage.Text);

        // 2. Instructions for the model. Kept identical from turn to turn so Ollama can reuse its cached work
        //    for the start of the prompt instead of re-reading it on every call.
        var systemPrompt = SystemPrompt(settings.SystemPrompt, name, HospitalTime.Today(_time), _hospitalRepo.GetInfo().Phone);
        var messages = new List<OllamaMessage> { new("system", systemPrompt) };

        // 3. Recent conversation, so the model remembers context. Earlier turns get their tool calls and results
        //    back, so the facts looked up then are still available. The retrieved knowledge is attached to the
        //    latest message only (augment), which keeps everything before it unchanged and cacheable.
        foreach (var message in history)
        {
            var isLatest = message.Id == patientMessage.Id;
            messages.Add(new(
                message.Sender == "patient" ? "user" : "assistant",
                isLatest ? WithKnowledge(message.Text, knowledge) : message.Text));
            if (message.Sender == "patient" && !isLatest)
            {
                messages.AddRange(_transcripts.For(patientMessage.PatientId, message.Id));
            }
        }

        // 4. Generate, running any tools the model asks for, until it answers with text.
        var context = new ToolContext(patientMessage.PatientId, patientMessage.Id);
        var toolMessages = new List<OllamaMessage>();
        var reply = await GenerateAsync(messages, context, toolMessages, draft, settings.MaxToolRounds);

        // 5. Check the reply for made-up doctors, prices and times (unless turned off in Settings). If there are any,
        //    tell the model and let it try once more. What the model was given (tool results, knowledge, the
        //    conversation) counts as known.
        var seenTexts = messages.Select(message => message.Content).ToList();
        IEnumerable<string> TurnToolResults() => toolMessages.Where(message => message.Role == "tool").Select(message => message.Content);
        var problems = reply is null || !settings.FactCheck ? [] : _factChecker.FindProblems(reply, seenTexts, TurnToolResults());
        if (problems.Count > 0)
        {
            _logger.LogWarning("Reply failed the fact check, retrying: {Problems}", string.Join(" ", problems));
            if (draft is not null)
            {
                await draft.ClearAsync();   // take the wrong draft off the screen while the model rewrites it
            }

            messages.Add(new("assistant", reply!));
            messages.Add(new("user", FactCheckCorrection(problems)));
            var toolMessagesBeforeRetry = toolMessages.Count;
            reply = await GenerateAsync(messages, context, toolMessages, draft, settings.MaxToolRounds);

            // The rejected draft and the correction quote the made-up facts, so they don't count as known;
            // only what was known before plus any tools called during the retry.
            seenTexts.AddRange(toolMessages.Skip(toolMessagesBeforeRetry).Select(message => message.Content));
            if (reply is not null && _factChecker.FindProblems(reply, seenTexts, TurnToolResults()) is { Count: > 0 } stillWrong)
            {
                _logger.LogWarning("Reply failed the fact check again; using a safe reply instead: {Problems}", string.Join(" ", stillWrong));
                // Never leave the patient believing they are booked when they aren't.
                reply = stillWrong.Any(problem => problem.StartsWith(ReplyFactChecker.FalseBookingProblem, StringComparison.Ordinal))
                    ? NotBookedReply
                    : settings.UncheckedReply;
            }
        }

        _transcripts.Save(patientMessage.PatientId, patientMessage.Id, toolMessages, settings.ToolHistoryTurns);
        return new AssistantReply(reply ?? settings.UnavailableReply, context.SlotsShown);
    }

    // Calls the model, running any tools it asks for, until it answers with text. Tool calls and results are added
    // to `messages` (for the model) and to `toolMessages` (to remember for later turns). Null if it is still calling
    // tools after `maxToolRounds`, which stops a confused model from looping.
    private async Task<string?> GenerateAsync(
        List<OllamaMessage> messages, ToolContext context, List<OllamaMessage> toolMessages, IReplyDraft? draft, int maxToolRounds)
    {
        var toolDefinitions = _tools.Values
            .Select(tool => new OllamaTool("function", new OllamaFunction(tool.Name, tool.Description, tool.Parameters)))
            .ToList();

        for (var round = 0; round < maxToolRounds; round++)
        {
            var reply = await ChatAsync(messages, toolDefinitions, draft);
            if (reply.ToolCalls is not { Count: > 0 })
            {
                return string.IsNullOrWhiteSpace(reply.Content) ? "Sorry, I couldn't think of a reply." : reply.Content.Trim();
            }

            // Keep the model's tool request in the conversation, then add each tool's result after it.
            messages.Add(reply);
            toolMessages.Add(reply);
            foreach (var call in reply.ToolCalls)
            {
                var result = new OllamaMessage("tool", await RunToolAsync(call.Function, context), ToolName: call.Function.Name);
                messages.Add(result);
                toolMessages.Add(result);
            }
        }

        _logger.LogWarning("Model was still calling tools after {Rounds} rounds", maxToolRounds);
        return null;
    }

    // Sent as a message from the "user" side because small models follow those more reliably than extra system text.
    internal static string FactCheckCorrection(IReadOnlyList<string> problems) => $"""
        [Automatic check, not from the patient] Your last reply contains facts that are not true for this hospital:
        {string.Join("\n", problems.Select(problem => $"- {problem}"))}
        Use your tools to look up the correct facts, then write the reply to the patient again.
        """;

    // Streams the model's answer: Ollama sends one JSON object per line, each with the next piece of text, and a final
    // line with the tool calls (if any) and timings. Text is shown in the draft as it arrives.
    private async Task<OllamaMessage> ChatAsync(IReadOnlyList<OllamaMessage> messages, IReadOnlyList<OllamaTool> tools, IReplyDraft? draft)
    {
        var settings = _settings.Get();
        var request = new OllamaChatRequest(
            settings.ChatModel, messages, Stream: true, tools.Count == 0 ? null : tools,
            new OllamaRequestOptions(settings.ContextSize, settings.Temperature), settings.KeepAlive);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat") { Content = JsonContent.Create(request) };
        // ResponseHeadersRead: start reading as soon as Ollama starts sending, instead of waiting for the whole reply.
        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var content = new StringBuilder();
        var toolCalls = new List<OllamaToolCall>();
        OllamaChatResponse? last = null;
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) || JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonSerializerOptions.Web) is not { } chunk)
            {
                continue;
            }

            last = chunk;
            if (chunk.Message.ToolCalls is { Count: > 0 } calls)
            {
                toolCalls.AddRange(calls);
            }

            if (!string.IsNullOrEmpty(chunk.Message.Content))
            {
                content.Append(chunk.Message.Content);
                if (draft is not null && toolCalls.Count == 0)
                {
                    await draft.ShowAsync(content.ToString());
                }
            }
        }

        // The model decided to call a tool after all, so whatever it wrote isn't the answer.
        if (draft is not null && toolCalls.Count > 0 && content.Length > 0)
        {
            await draft.ClearAsync();
        }

        if (last is not null)
        {
            _logger.LogInformation(
                "Ollama call: {Messages} messages | load {Load:F1}s | prompt {PromptTokens} tokens in {PromptSeconds:F1}s | reply {ReplyTokens} tokens in {ReplySeconds:F1}s",
                messages.Count, last.LoadDuration / 1e9, last.PromptEvalCount, last.PromptEvalDuration / 1e9,
                last.EvalCount, last.EvalDuration / 1e9);
        }
        return new OllamaMessage("assistant", content.ToString(), toolCalls.Count > 0 ? toolCalls : null);
    }

    private async Task<string> RunToolAsync(OllamaFunctionCall call, ToolContext context)
    {
        if (!_tools.TryGetValue(call.Name, out var tool))
        {
            _logger.LogWarning("Model asked for unknown tool {Tool}", call.Name);
            return $"Error: there is no tool called '{call.Name}'. Available tools: {string.Join(", ", _tools.Keys)}.";
        }

        try
        {
            var result = await tool.RunAsync(call.Arguments, context, CancellationToken.None);
            _logger.LogInformation("Tool {Tool}({Arguments}) returned {Length} chars", call.Name, call.Arguments.GetRawText(), result.Length);
            return result;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or JsonException or FormatException)
        {
            // Let the model read the problem and correct itself, instead of failing the whole reply.
            _logger.LogInformation("Tool {Tool}({Arguments}) failed: {Error}", call.Name, call.Arguments.GetRawText(), ex.Message);
            return $"Error: {ex.Message}";
        }
    }

    // Searches with the patient's latest message only. Earlier messages, and especially the assistant's long
    // replies, would pull the search back to the previous topic. The model still sees the whole conversation,
    // so it can resolve follow-ups like "how much does that doctor charge?".
    private async Task<IReadOnlyList<SearchResult>> RetrieveAsync(string query)
    {
        try
        {
            var results = await _retriever.SearchAsync(query);
            _logger.LogInformation("Retrieved {Count} knowledge chunks: {Ids}",
                results.Count, string.Join(", ", results.Select(result => $"{result.Document.Id} ({result.Score:F3})")));
            return results;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            // Still answer without knowledge rather than failing the whole reply.
            _logger.LogWarning(ex, "Knowledge retrieval failed; replying without it");
            return [];
        }
    }

    // Keeps at most `max` recent messages. Trims in steps of half that size rather than one message at a time,
    // so the start of the prompt stays the same for several turns and Ollama's cache keeps working.
    internal static IReadOnlyList<ChatMessage> RecentHistory(IReadOnlyList<ChatMessage> history, int max)
    {
        if (history.Count <= max)
        {
            return history;
        }

        var step = Math.Max(1, max / 2);
        var start = ((history.Count - max - 1) / step + 1) * step;
        return history.Skip(start).ToList();
    }

    // Fills in the System prompt setting's placeholders. Same output all day, so Ollama can keep reusing its cache.
    internal static string SystemPrompt(string template, string patientName, DateOnly today, string hospitalPhone)
    {
        var calendar = string.Join("\n", Enumerable.Range(1, 7).Select(offset => today.AddDays(offset)).Select((date, i) =>
            $"- {(i == 0 ? "tomorrow, " : "")}{date:dddd d MMMM} = {date:yyyy-MM-dd}"));
        return template
            .Replace("{patient_name}", patientName)
            .Replace("{today}", $"{today:dddd, d MMMM yyyy} ({today:yyyy-MM-dd})")
            .Replace("{calendar}", calendar)
            .Replace("{hospital_phone}", string.IsNullOrWhiteSpace(hospitalPhone) ? "the hospital's phone number" : hospitalPhone);
    }

    // Attaches the retrieved knowledge to the patient's latest message (the "augment" step of RAG).
    internal static string WithKnowledge(string patientText, IReadOnlyList<SearchResult> knowledge)
    {
        var information = knowledge.Count == 0
            ? "(No hospital information matched this question.)"
            : string.Join("\n\n", knowledge.Select((result, i) => $"[{i + 1}] {result.Document.Text}"));

        return $"""
            HOSPITAL INFORMATION:
            {information}

            PATIENT MESSAGE:
            {patientText}
            """;
    }
}
