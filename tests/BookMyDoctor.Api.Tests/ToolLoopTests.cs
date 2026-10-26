using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BookMyDoctor.Features.Chats;
using BookMyDoctor.Features.Chats.Tools;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Features.Knowledge;
using BookMyDoctor.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class ToolLoopTests
{
    [Fact]
    public async Task RunsRequestedToolAndSendsResultBackToModel()
    {
        var ollama = new ScriptedOllama(
            ToolCall("get_doctors", """{"specialty":"Dermatology"}"""),
            Text("Doctor Dermatology speaks Spanish."));
        var (assistant, message) = Assistant(ollama, "Which skin doctor speaks Spanish?");

        var reply = await assistant.ReplyAsync(message);

        Assert.Equal("Doctor Dermatology speaks Spanish.", reply);
        Assert.Equal(2, ollama.Requests.Count);

        // First request offers the tool to the model, in Ollama's JSON format.
        Assert.Contains("\"tools\":[{\"type\":\"function\"", ollama.RawRequests[0]);
        Assert.Contains("\"options\":{\"num_ctx\":8192,", ollama.RawRequests[0]);
        Assert.Contains("\"keep_alive\":\"30m\"", ollama.RawRequests[0]);

        // Knowledge goes with the latest patient message, not in the system prompt.
        Assert.DoesNotContain("HOSPITAL INFORMATION:", ollama.Requests[0].Messages[0].Content);
        Assert.Contains("PATIENT MESSAGE:\nWhich skin doctor speaks Spanish?", ollama.Requests[0].Messages[^1].Content);
        Assert.Contains("\"name\":\"get_doctors\"", ollama.RawRequests[0]);

        // Second request repeats the model's tool call, then the tool's result.
        var secondMessages = ollama.Requests[1].Messages;
        Assert.Equal("get_doctors", secondMessages[^2].ToolCalls!.Single().Function.Name);
        Assert.Equal("tool", secondMessages[^1].Role);
        Assert.Equal("get_doctors", secondMessages[^1].ToolName);
        Assert.Contains("Doctor Dermatology | Dermatology", secondMessages[^1].Content);
        Assert.DoesNotContain("Doctor Cardiology", secondMessages[^1].Content);
        Assert.Contains("\"tool_calls\":[", ollama.RawRequests[1]);
    }

    [Fact]
    public async Task UnknownToolIsReportedBackToTheModel()
    {
        var ollama = new ScriptedOllama(ToolCall("get_weather", "{}"), Text("Sorry, I can't check the weather."));
        var (assistant, message) = Assistant(ollama, "Will it rain?");

        await assistant.ReplyAsync(message);

        var toolResult = ollama.Requests[1].Messages[^1];
        Assert.Equal("tool", toolResult.Role);
        Assert.Contains("no tool called 'get_weather'", toolResult.Content);
    }

    [Fact]
    public async Task GivesUpAfterMaxToolRounds()
    {
        var calls = Enumerable.Repeat(ToolCall("get_doctors", "{}"), OllamaChatAssistant.MaxToolRounds).ToArray();
        var ollama = new ScriptedOllama(calls);
        var (assistant, message) = Assistant(ollama, "Doctors?");

        var reply = await assistant.ReplyAsync(message);

        Assert.Equal(OllamaChatAssistant.MaxToolRounds, ollama.Requests.Count);
        Assert.Contains("having trouble", reply);
    }

    [Fact]
    public async Task StreamsTheAnswerIntoTheDraftWordByWord()
    {
        var ollama = new ScriptedOllama(Text("Hello Hari, how can I help?"));
        var (assistant, message) = Assistant(ollama, "Hi");
        var draft = new RecordingDraft();

        var reply = await assistant.ReplyAsync(message, draft);

        Assert.Equal("Hello Hari, how can I help?", reply);
        Assert.Contains("\"stream\":true", ollama.RawRequests[0]);
        Assert.Equal(["Hello ", "Hello Hari, ", "Hello Hari, how ", "Hello Hari, how can ", "Hello Hari, how can I ", "Hello Hari, how can I help?"],
            draft.Events);
    }

    [Fact]
    public async Task DraftThatFailsTheFactCheckIsClearedBeforeTheRewrite()
    {
        var ollama = new ScriptedOllama(Text("Dr. Ravi Kumar, $150."), Text("Doctor Cardiology, $180."));
        var (assistant, message) = Assistant(ollama, "Who is the cardiologist?");
        var draft = new RecordingDraft();

        var reply = await assistant.ReplyAsync(message, draft);

        Assert.Equal("Doctor Cardiology, $180.", reply);
        var clear = draft.Events.IndexOf("<clear>");
        Assert.Equal("Dr. Ravi Kumar, $150.", draft.Events[clear - 1]);   // the wrong draft was shown...
        Assert.Equal("Doctor Cardiology, $180.", draft.Events[^1]);       // ...then removed and rewritten
    }

    [Fact]
    public async Task SendsLowTemperature()
    {
        var ollama = new ScriptedOllama(Text("Hello!"));
        var (assistant, message) = Assistant(ollama, "Hi");

        await assistant.ReplyAsync(message);

        Assert.Contains("\"temperature\":0.2", ollama.RawRequests[0]);
    }

    [Fact]
    public async Task ToolResultsFromThePreviousTurnAreSentAgain()
    {
        var ollama = new ScriptedOllama(
            ToolCall("get_doctors", """{"specialty":"Cardiology"}"""), Text("Doctor Cardiology is our heart doctor."),
            Text("Doctor Cardiology charges $180."));
        var chatService = new ChatService(new ChatsRepo());
        var assistant = Build(ollama, chatService);

        // Turn 1 looks up the doctor; the hub saves the reply.
        var first = chatService.Send(new SendMessageRequest(SeedIds.HariPatientId, "patient", "Who is the heart doctor?"));
        chatService.Send(new SendMessageRequest(SeedIds.HariPatientId, "assistant", await assistant.ReplyAsync(first)));

        // Turn 2 asks a follow-up; the model answers without calling a tool.
        var second = chatService.Send(new SendMessageRequest(SeedIds.HariPatientId, "patient", "How much does he charge?"));
        await assistant.ReplyAsync(second);

        // The request for turn 2 still contains turn 1's tool call and result, between the question and the reply.
        var messages = ollama.Requests[2].Messages;
        var question = messages.ToList().FindIndex(m => m.Content == "Who is the heart doctor?");
        Assert.Equal("get_doctors", messages[question + 1].ToolCalls!.Single().Function.Name);
        Assert.Equal("tool", messages[question + 2].Role);
        Assert.Contains("new visit $180", messages[question + 2].Content);
        Assert.Equal("Doctor Cardiology is our heart doctor.", messages[question + 3].Content);
    }

    [Fact]
    public async Task InventedDoctorOrPriceMakesTheModelTryAgain()
    {
        var ollama = new ScriptedOllama(
            Text("Dr. Ravi Kumar can see you; the fee is $150."),
            Text("Doctor Cardiology can see you; the fee is $180."));
        var (assistant, message) = Assistant(ollama, "Who is the cardiologist?");

        var reply = await assistant.ReplyAsync(message);

        Assert.Equal("Doctor Cardiology can see you; the fee is $180.", reply);
        var correction = ollama.Requests[1].Messages[^1];
        Assert.Equal("user", correction.Role);
        Assert.Contains("\"Dr. Ravi Kumar\" is not a doctor at this hospital", correction.Content);
        Assert.Contains("\"$150\" is not a real fee or price", correction.Content);
    }

    [Fact]
    public async Task InventedTimeMakesTheModelLookItUp()
    {
        var ollama = new ScriptedOllama(
            Text("The cardiologist is free tomorrow at 09:00."),
            ToolCall("get_doctors", """{"specialty":"Cardiology"}"""),
            Text("Doctor Cardiology is our cardiologist. Shall I check his free times?"));
        var (assistant, message) = Assistant(ollama, "When can I see the cardiologist?");

        var reply = await assistant.ReplyAsync(message);

        Assert.Equal("Doctor Cardiology is our cardiologist. Shall I check his free times?", reply);
        Assert.Contains("\"09:00\" is not a time you looked up", ollama.Requests[1].Messages[^1].Content);
    }

    [Fact]
    public async Task RejectedDraftDoesNotCountAsHavingSeenItsOwnMadeUpTime()
    {
        // The second answer repeats the made-up time; it appears in the rejected draft and the correction,
        // but no tool returned it, so it must still be rejected.
        var ollama = new ScriptedOllama(
            Text("The cardiologist is free tomorrow at 09:00."),
            Text("Yes, 09:00 tomorrow works."));
        var (assistant, message) = Assistant(ollama, "When can I see the cardiologist?");

        var reply = await assistant.ReplyAsync(message);

        Assert.StartsWith("Sorry, I couldn't check that properly.", reply);
    }

    [Fact]
    public async Task InventedFactsTwiceGiveASafeReply()
    {
        var ollama = new ScriptedOllama(Text("Dr. Ravi Kumar, $150."), Text("Dr. Ravi Kumar again, $150."));
        var (assistant, message) = Assistant(ollama, "Who is the cardiologist?");

        var reply = await assistant.ReplyAsync(message);

        Assert.StartsWith("Sorry, I couldn't check that properly.", reply);
        Assert.DoesNotContain("Ravi", reply);
    }

    private static (OllamaChatAssistant, ChatMessage) Assistant(ScriptedOllama ollama, string patientText)
    {
        var chatService = new ChatService(new ChatsRepo());
        var message = chatService.Send(new SendMessageRequest(SeedIds.HariPatientId, "patient", patientText));
        return (Build(ollama, chatService), message);
    }

    private static OllamaChatAssistant Build(ScriptedOllama ollama, ChatService chatService)
    {
        var doctorService = new DoctorService(new DoctorsRepo());
        return new OllamaChatAssistant(
            new HttpClient(ollama) { BaseAddress = new Uri("http://ollama.test") },
            Options.Create(new OllamaOptions { Model = "test-model" }),
            chatService,
            new UsersRepo(),
            new NoKnowledge(),
            [new GetDoctorsTool(doctorService)],
            new ToolTranscripts(),
            new ReplyFactChecker(doctorService, new HospitalRepo()),
            TimeProvider.System,
            NullLogger<OllamaChatAssistant>.Instance);
    }

    private static OllamaMessage Text(string content) => new("assistant", content);

    private static OllamaMessage ToolCall(string name, string argumentsJson) =>
        new("assistant", "", [new OllamaToolCall(new OllamaFunctionCall(name, JsonDocument.Parse(argumentsJson).RootElement))]);

    private sealed class NoKnowledge : IKnowledgeRetriever
    {
        public Task<IReadOnlyList<SearchResult>> SearchAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchResult>>([]);
    }

    // Collects what the assistant shows while writing: each draft text, and "<clear>" when it removes the draft.
    private sealed class RecordingDraft : IReplyDraft
    {
        public List<string> Events { get; } = [];

        public Task ShowAsync(string textSoFar)
        {
            Events.Add(textSoFar);
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            Events.Add("<clear>");
            return Task.CompletedTask;
        }
    }

    // Plays back prepared model replies in order and records every request it receives.
    // Replies are streamed like Ollama does: one JSON line per word, then a final line with any tool calls.
    private sealed class ScriptedOllama(params OllamaMessage[] replies) : HttpMessageHandler
    {
        private readonly Queue<OllamaMessage> _replies = new(replies);

        public List<string> RawRequests { get; } = [];
        public List<OllamaChatRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var raw = await request.Content!.ReadAsStringAsync(cancellationToken);
            RawRequests.Add(raw);
            Requests.Add(JsonSerializer.Deserialize<OllamaChatRequest>(raw, JsonSerializerOptions.Web)!);

            var reply = _replies.Dequeue();
            var lines = Regex.Matches(reply.Content, @"\S+\s*")
                .Select(word => new OllamaChatResponse(new OllamaMessage("assistant", word.Value), false))
                .Append(new OllamaChatResponse(new OllamaMessage("assistant", "", reply.ToolCalls), true))
                .Select(chunk => JsonSerializer.Serialize(chunk, JsonSerializerOptions.Web));
            return new HttpResponseMessage { Content = new StringContent(string.Join("\n", lines) + "\n") };
        }
    }
}
