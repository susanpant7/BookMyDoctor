using System.Text.Json;
using MyHospitalAI.Features.Appointments;
using MyHospitalAI.Features.Chats.Tools;
using MyHospitalAI.Features.Knowledge;
using MyHospitalAI.Features.Settings;
using MyHospitalAI.Persistence;

namespace MyHospitalAI.Features.Chats;

public sealed class ChatAssistant : IChatAssistant
{
    // Sent when the model keeps claiming a booking that didn't happen.
    internal const string NotBookedReply =
        "Sorry, your appointment is not booked yet. Please pick a time from the buttons and confirm it, or ask me for free times again.";

    private readonly IChatModel _model;
    private readonly IAiSettingsProvider _settings;
    private readonly IChatService _chatService;
    private readonly IUsersRepo _usersRepo;
    private readonly IHospitalRepo _hospitalRepo;
    private readonly IKnowledgeRetriever _retriever;
    private readonly IReadOnlyDictionary<string, IAssistantTool> _tools;
    private readonly ToolTranscripts _transcripts;
    private readonly ReplyFactChecker _factChecker;
    private readonly TimeProvider _time;
    private readonly ILogger<ChatAssistant> _logger;

    public ChatAssistant(
        IChatModel model,
        IAiSettingsProvider settings,
        IChatService chatService,
        IUsersRepo usersRepo,
        IHospitalRepo hospitalRepo,
        IKnowledgeRetriever retriever,
        IEnumerable<IAssistantTool> tools,
        ToolTranscripts transcripts,
        ReplyFactChecker factChecker,
        TimeProvider time,
        ILogger<ChatAssistant> logger)
    {
        _model = model;
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
        var knowledge = await RetrieveAsync(patientMessage, history, settings);

        // 2. Instructions for the model. Kept identical from turn to turn so the model (Ollama, or the online
        //    provider's prompt cache) can reuse its work for the start of the prompt instead of re-reading it.
        var systemPrompt = SystemPrompt(settings.SystemPrompt, name, HospitalTime.Today(_time), _hospitalRepo.GetInfo().Phone);
        var messages = new List<ModelMessage> { new("system", systemPrompt) };

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
        var context = new ToolContext(patientMessage.PatientId, patientMessage.Id, patientMessage.Text);
        var toolMessages = new List<ModelMessage>();
        var reply = await GenerateAsync(messages, context, toolMessages, draft, settings);

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
            reply = await GenerateAsync(messages, context, toolMessages, draft, settings);

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
                // The free times belonged to the rejected reply, so no time buttons under the safe one.
                context.SlotsShown = null;
            }
        }

        _transcripts.Save(patientMessage.PatientId, patientMessage.Id, toolMessages, settings.ToolHistoryTurns);
        return reply is null
            ? new AssistantReply(settings.UnavailableReply)
            : new AssistantReply(reply, context.SlotsShown);
    }

    // Calls the model, running any tools it asks for, until it answers with text. Tool calls and results are added
    // to `messages` (for the model) and to `toolMessages` (to remember for later turns). Null if it is still calling
    // tools after `maxToolRounds`, which stops a confused model from looping.
    private async Task<string?> GenerateAsync(
        List<ModelMessage> messages, ToolContext context, List<ModelMessage> toolMessages, IReplyDraft? draft, AiSettings settings)
    {
        var toolDefinitions = _tools.Values
            .Select(tool => new ModelTool("function", new ModelFunction(tool.Name, tool.Description, tool.Parameters)))
            .ToList();

        for (var round = 0; round < settings.MaxToolRounds; round++)
        {
            var reply = await _model.ChatAsync(messages, toolDefinitions, settings, draft is null ? null : draft.ShowAsync);
            // The model decided to call a tool after all, so whatever it wrote isn't the answer.
            if (draft is not null && reply.ToolCalls is { Count: > 0 } && reply.Content.Length > 0)
            {
                await draft.ClearAsync();
            }

            if (reply.ToolCalls is not { Count: > 0 })
            {
                return string.IsNullOrWhiteSpace(reply.Content) ? "Sorry, I couldn't think of a reply." : reply.Content.Trim();
            }

            // Keep the model's tool request in the conversation, then add each tool's result after it.
            messages.Add(reply);
            toolMessages.Add(reply);
            foreach (var call in reply.ToolCalls)
            {
                var result = new ModelMessage("tool", await RunToolAsync(call.Function, context), ToolName: call.Function.Name, ToolCallId: call.Id);
                messages.Add(result);
                toolMessages.Add(result);
            }
        }

        _logger.LogWarning("Model was still calling tools after {Rounds} rounds", settings.MaxToolRounds);
        return null;
    }

    // Sent as a message from the "user" side because small models follow those more reliably than extra system text.
    internal static string FactCheckCorrection(IReadOnlyList<string> problems) => $"""
        [Automatic check, not from the patient] Your last reply contains facts that are not true for this hospital:
        {string.Join("\n", problems.Select(problem => $"- {problem}"))}
        Use your tools to look up the correct facts, then write the reply to the patient again.
        """;

    private async Task<string> RunToolAsync(ModelFunctionCall call, ToolContext context)
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

    // Searches with the patient's latest message. Earlier messages, and especially the assistant's long replies, would
    // pull the search back to the previous topic, so they are not added to it. But a short message can be a follow-up
    // that means nothing alone ("does he speak Spanish?"), so then the chat model first writes it as one complete
    // question from the last messages (a message that stands alone comes back unchanged), and both are searched.
    // A section found by both counts with its better score.
    private async Task<IReadOnlyList<SearchResult>> RetrieveAsync(ChatMessage latest, IReadOnlyList<ChatMessage> history, AiSettings settings)
    {
        var queries = new List<string> { latest.Text };
        var earlier = history.Where(message => message.Id != latest.Id).ToList();
        var isShort = latest.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= settings.RetrievalFollowUpMaxWords;
        if (settings.RewriteFollowUps && isShort && earlier.Any(message => message.Sender == "patient"))
        {
            var rewritten = await RewriteAsync(latest.Text, earlier, settings);
            if (rewritten is not null && !rewritten.Equals(latest.Text, StringComparison.OrdinalIgnoreCase))
            {
                queries.Insert(0, rewritten);
            }
        }

        try
        {
            var best = new Dictionary<string, SearchResult>();
            foreach (var query in queries)
            {
                foreach (var result in await _retriever.SearchAsync(query))
                {
                    if (!best.TryGetValue(result.Document.Id, out var seen) || result.Score > seen.Score)
                    {
                        best[result.Document.Id] = result;
                    }
                }
            }

            var results = best.Values.OrderByDescending(result => result.Score).ToList();
            if (results.Count > 0)
            {
                results = results.Where(result => result.Score >= results[0].Score - settings.RetrievalMaxScoreGap)
                    .Take(settings.RetrievalTop).ToList();
            }
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

    private const int RewriteContextMessages = 4;
    private const int RewriteContextChars = 300;
    private static readonly TimeSpan RewriteTimeout = TimeSpan.FromSeconds(15);

    // Asks the chat model to write the message as one complete question, from the last messages (each cut short: a
    // reply can be long, and only its topic matters here). Null when that did not work (a failure, a timeout, or a
    // reply that is not a question), so the message is searched as it is.
    private async Task<string?> RewriteAsync(string text, IReadOnlyList<ChatMessage> earlier, AiSettings settings)
    {
        var conversation = string.Join("\n", earlier.TakeLast(RewriteContextMessages).Select(message =>
        {
            var shown = message.Text.Length > RewriteContextChars ? message.Text[..RewriteContextChars] + "..." : message.Text;
            return $"{(message.Sender == "patient" ? "Patient" : "Assistant")}: {shown}";
        }));
        var messages = new List<ModelMessage>
        {
            new("system", settings.RewritePrompt),
            new("user", $"Conversation:\n{conversation}\n\nLast message: {text}")
        };

        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var reply = await _model.ChatAsync(messages, [], settings with { Temperature = 0 }, null).WaitAsync(RewriteTimeout);

            // Only the first line, without quotes: the instruction asks for the question alone.
            var question = reply.Content.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim().Trim('"', '\'', '`') ?? "";
            if (question.Length is 0 or > 300)
            {
                return null;
            }

            _logger.LogInformation("Search query rewritten in {Ms} ms: \"{Message}\" -> \"{Question}\"", clock.ElapsedMilliseconds, text, question);
            return question;
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException)
        {
            _logger.LogWarning("The follow-up was not rewritten ({Error}); it is searched as it is", ex.Message);
            return null;
        }
    }

    // Keeps at most `max` recent messages. Trims in steps of half that size rather than one message at a time,
    // so the start of the prompt stays the same for several turns and the model's cache keeps working.
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

    // Fills in the System prompt setting's placeholders. Same output all day, so the model can keep reusing its cache.
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
