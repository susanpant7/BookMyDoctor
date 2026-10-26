using System.Text;
using System.Text.Json;
using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Chats.Tools;
using BookMyDoctor.Features.Knowledge;
using BookMyDoctor.Persistence;
using Microsoft.Extensions.Options;

namespace BookMyDoctor.Features.Chats;

public sealed class OllamaChatAssistant : IChatAssistant
{
    // Stops a confused model from calling tools forever.
    internal const int MaxToolRounds = 5;

    private readonly HttpClient _http;
    private readonly OllamaOptions _options;
    private readonly IChatService _chatService;
    private readonly IUsersRepo _usersRepo;
    private readonly IKnowledgeRetriever _retriever;
    private readonly IReadOnlyDictionary<string, IAssistantTool> _tools;
    private readonly ToolTranscripts _transcripts;
    private readonly ReplyFactChecker _factChecker;
    private readonly TimeProvider _time;
    private readonly ILogger<OllamaChatAssistant> _logger;

    public OllamaChatAssistant(
        HttpClient http,
        IOptions<OllamaOptions> options,
        IChatService chatService,
        IUsersRepo usersRepo,
        IKnowledgeRetriever retriever,
        IEnumerable<IAssistantTool> tools,
        ToolTranscripts transcripts,
        ReplyFactChecker factChecker,
        TimeProvider time,
        ILogger<OllamaChatAssistant> logger)
    {
        _http = http;
        _options = options.Value;
        _chatService = chatService;
        _usersRepo = usersRepo;
        _retriever = retriever;
        _tools = tools.ToDictionary(tool => tool.Name);
        _transcripts = transcripts;
        _factChecker = factChecker;
        _time = time;
        _logger = logger;
    }

    public async Task<string> ReplyAsync(ChatMessage patientMessage, IReplyDraft? draft = null)
    {
        var name = _usersRepo.GetById(patientMessage.PatientId)?.Name ?? "there";
        // The patient's new message was already saved by the hub, so it's the last one here.
        var history = RecentHistory(_chatService.GetMessages(patientMessage.PatientId), _options.MaxHistoryMessages);

        // 1. Retrieve: find the knowledge chunks most related to what the patient just asked.
        var knowledge = await RetrieveAsync(patientMessage.Text);

        // 2. Instructions for the model. Kept identical from turn to turn so Ollama can reuse its cached work
        //    for the start of the prompt instead of re-reading it on every call.
        var messages = new List<OllamaMessage> { new("system", SystemPrompt(name, HospitalTime.Today(_time))) };

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
        var reply = await GenerateAsync(messages, context, toolMessages, draft);

        // 5. Check the reply for made-up doctors, prices and times. If there are any, tell the model and let it
        //    try once more. What the model was given (tool results, knowledge, the conversation) counts as known.
        var seenTexts = messages.Select(message => message.Content).ToList();
        var problems = reply is null ? [] : _factChecker.FindProblems(reply, seenTexts);
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
            reply = await GenerateAsync(messages, context, toolMessages, draft);

            // The rejected draft and the correction quote the made-up facts, so they don't count as known;
            // only what was known before plus any tools called during the retry.
            seenTexts.AddRange(toolMessages.Skip(toolMessagesBeforeRetry).Select(message => message.Content));
            if (reply is not null && _factChecker.FindProblems(reply, seenTexts) is { Count: > 0 } stillWrong)
            {
                _logger.LogWarning("Reply failed the fact check again; using a safe reply instead: {Problems}", string.Join(" ", stillWrong));
                reply = "Sorry, I couldn't check that properly. You can see every doctor, their fees and their details on the Doctors page.";
            }
        }

        _transcripts.Save(patientMessage.PatientId, patientMessage.Id, toolMessages, _options.ToolHistoryTurns);
        return reply ?? "Sorry, I'm having trouble looking that up right now. Please try again.";
    }

    // Calls the model, running any tools it asks for, until it answers with text. Tool calls and results are added
    // to `messages` (for the model) and to `toolMessages` (to remember for later turns). Null if it never answers.
    private async Task<string?> GenerateAsync(
        List<OllamaMessage> messages, ToolContext context, List<OllamaMessage> toolMessages, IReplyDraft? draft)
    {
        var toolDefinitions = _tools.Values
            .Select(tool => new OllamaTool("function", new OllamaFunction(tool.Name, tool.Description, tool.Parameters)))
            .ToList();

        for (var round = 0; round < MaxToolRounds; round++)
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

        _logger.LogWarning("Model was still calling tools after {Rounds} rounds", MaxToolRounds);
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
        var request = new OllamaChatRequest(
            _options.Model, messages, Stream: true, tools.Count == 0 ? null : tools,
            new OllamaRequestOptions(_options.ContextSize, _options.Temperature), _options.KeepAlive);
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
        catch (HttpRequestException ex)
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

    internal static string SystemPrompt(string patientName, DateOnly today) => $"""
        You are Vaidya Mitra, a friendly assistant for BookMyDoctor Hospital.
        You are talking to a patient named {patientName}. Help them find the right specialty and doctor, and answer questions about the hospital.
        Today is {today:dddd, d MMMM yyyy} ({today:yyyy-MM-dd}).
        Use this calendar for dates; don't work them out yourself:
        {string.Join("\n", Enumerable.Range(1, 7).Select(offset => today.AddDays(offset)).Select((date, i) =>
            $"- {(i == 0 ? "tomorrow, " : "")}{date:dddd d MMMM} = {date:yyyy-MM-dd}"))}
        Do not give medical diagnoses or prescribe medicine.

        Rules:
        - For doctors, fees, languages, appointment times, the patient's own appointments and other hospital data, use your tools
          to look up the current facts. Never guess them.
        - Never ask the patient for permission to look something up and never say you will check it; call the tool straight away
          and answer with what it returns. If a specialty is mentioned (e.g. a cardiologist), look up that specialty's doctor.
        - When you share free times, write up to four of them in your reply; don't end with an empty list.
        - For policies and medical guidance, use the HOSPITAL INFORMATION attached to the patient's latest message.
        - Never invent doctors, policies, prices or times. If you can't find the answer, say so and suggest contacting the hospital.
        - If the patient describes an emergency warning sign, tell them to call 911 first.
        - Keep replies to two or three short sentences. When offering appointment times, suggest at most four and ask which suits.
        - When the patient picks one of the times you showed, call book_appointment with that time and their health problem
          as the reason (don't look up the times again). Then ask the patient to confirm the summary it gives you.
          When they say yes, call book_appointment with no arguments.
        - Never say an appointment is booked unless book_appointment replied "Booked", and then give the patient the reference.
        - Never mention tools, functions or the information block to the patient; just answer naturally.
        """;

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
