using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MyHospitalAI.Features.Settings;

namespace MyHospitalAI.Features.Chats;

// A model run by an online service (Gemini, Grok, Groq, OpenRouter, ...). They all accept the OpenAI chat format:
// POST {Chat API address}/chat/completions with "Authorization: Bearer <key>", and stream the reply as server-sent
// events. The key is the .env variable named by the "Chat API key name" setting (see AiProviders.KeyNameInUse); it
// is never logged or stored.
public sealed class OnlineChatModel : IChatModel
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OnlineChatModel> _logger;

    public OnlineChatModel(HttpClient http, IConfiguration configuration, ILogger<OnlineChatModel> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ModelMessage> ChatAsync(
        IReadOnlyList<ModelMessage> messages, IReadOnlyList<ModelTool> tools, AiSettings settings, Func<string, Task>? onText)
    {
        // Named by its host in the logs, e.g. api.groq.com.
        var service = new Uri(settings.ChatBaseUrl).Host;
        var keyName = AiProviders.KeyNameInUse(settings.ChatApiKeyName, settings.ChatBaseUrl);
        var key = AiProviders.ReadKey(_configuration, keyName);
        if (key is null)
        {
            _logger.LogWarning("The chat model is at {Service}, but it has no key: {Problem}", service, AiProviders.MissingKeyMessage(keyName));
            throw new HttpRequestException(AiProviders.MissingKeyMessage(keyName));
        }

        var body = new JsonObject
        {
            ["model"] = settings.ChatModel,
            ["stream"] = true,
            ["temperature"] = settings.Temperature,
            ["messages"] = ToRequestMessages(messages)
        };
        if (tools.Count > 0)
        {
            body["tools"] = JsonSerializer.SerializeToNode(tools, JsonSerializerOptions.Web);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.ChatBaseUrl}/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        var timer = Stopwatch.StartNew();
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("{Service} returned {Status} for model {Model}: {Problem} {Detail}",
                service, (int)response.StatusCode, settings.ChatModel, Explain(response.StatusCode, detail, keyName),
                detail.Length > 500 ? detail[..500] : detail);
            throw new HttpRequestException(
                $"{service} returned {(int)response.StatusCode}. {Explain(response.StatusCode, detail, keyName)} {AiProviders.ErrorDetail(detail)}".TrimEnd(),
                null, response.StatusCode);
        }

        // Each event is a line "data: {json}" with the next piece of text or of a tool call; the last is "data: [DONE]".
        // A tool call arrives in pieces too: its id and name first, then its arguments a few characters at a time.
        var content = new StringBuilder();
        var calls = new List<CallBuilder>();
        double? firstTextSeconds = null;
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        while (await reader.ReadLineAsync() is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data == "[DONE]")
            {
                break;
            }
            if (data.Length == 0 || JsonSerializer.Deserialize<StreamChunk>(data, JsonSerializerOptions.Web) is not { } chunk)
            {
                continue;
            }

            if (chunk.Error?.Message is { } error)
            {
                _logger.LogWarning("{Service} failed while answering: {Error}", service, error);
                throw new HttpRequestException($"{service} failed while answering: {error}");
            }

            var delta = chunk.Choices?.FirstOrDefault()?.Delta;
            foreach (var piece in delta?.ToolCalls ?? [])
            {
                CallFor(calls, piece).Append(piece);
            }

            if (!string.IsNullOrEmpty(delta?.Content))
            {
                firstTextSeconds ??= timer.Elapsed.TotalSeconds;
                content.Append(delta.Content);
                if (onText is not null && calls.Count == 0)
                {
                    await onText(content.ToString());
                }
            }
        }

        _logger.LogInformation(
            "{Service} call ({Model}): {Messages} messages | first text {FirstText} | done in {Seconds:F1}s | {ToolCalls} tool calls",
            service, settings.ChatModel, messages.Count, firstTextSeconds is { } first ? $"{first:F1}s" : "none",
            timer.Elapsed.TotalSeconds, calls.Count);
        return new ModelMessage("assistant", content.ToString(), calls.Count > 0 ? calls.Select(call => call.Build()).ToList() : null);
    }

    // The OpenAI format: a tool call's arguments are a JSON string, and a tool result names the call it answers.
    // Calls remembered from an earlier Ollama turn have no id, so they get one here, and their results get the same.
    internal static JsonArray ToRequestMessages(IReadOnlyList<ModelMessage> messages)
    {
        var result = new JsonArray();
        var unanswered = new Queue<string>();
        foreach (var message in messages)
        {
            if (message.Role == "tool")
            {
                var callId = message.ToolCallId ?? (unanswered.Count > 0 ? unanswered.Dequeue() : NewCallId());
                result.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = callId, ["content"] = message.Content });
                continue;
            }

            var item = new JsonObject { ["role"] = message.Role, ["content"] = message.Content };
            if (message.ToolCalls is { Count: > 0 } toolCalls)
            {
                unanswered.Clear();
                var calls = new JsonArray();
                foreach (var call in toolCalls)
                {
                    var id = call.Id ?? NewCallId();
                    if (call.Id is null)
                    {
                        unanswered.Enqueue(id);
                    }
                    var entry = new JsonObject
                    {
                        ["id"] = id,
                        ["type"] = "function",
                        ["function"] = new JsonObject { ["name"] = call.Function.Name, ["arguments"] = call.Function.Arguments.GetRawText() }
                    };
                    // Sent back exactly as the service gave it; Gemini refuses the conversation without its thought_signature.
                    if (call.Extra is { } extra)
                    {
                        entry["extra_content"] = JsonNode.Parse(extra.GetRawText());
                    }
                    calls.Add(entry);
                }
                item["tool_calls"] = calls;
                // Some services refuse an empty text next to tool calls.
                if (message.Content.Length == 0)
                {
                    item["content"] = null;
                }
            }
            result.Add(item);
        }
        return result;
    }

    // Short letters and digits only, because some services (Mistral) accept no other ids.
    private static string NewCallId() => Guid.NewGuid().ToString("N")[..9];

    // Which call a streamed piece belongs to: by its index, or (for services that send each call whole) a new one.
    private static CallBuilder CallFor(List<CallBuilder> calls, StreamToolCall piece)
    {
        var existing = piece.Index is { } index
            ? calls.FirstOrDefault(call => call.Index == index)
            : piece.Id is null ? calls.LastOrDefault() : calls.FirstOrDefault(call => call.Id == piece.Id);
        if (existing is not null)
        {
            return existing;
        }

        var added = new CallBuilder(piece.Index);
        calls.Add(added);
        return added;
    }

    // Gemini answers a wrong key with 400, so the reply is checked for "API key" too.
    private static string Explain(HttpStatusCode status, string detail, string keyName) => status switch
    {
        _ when detail.Contains("API key", StringComparison.OrdinalIgnoreCase) =>
            $"The key was refused: check that {keyName} in the .env file is a whole, valid key for this service, then restart the server.",
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            $"The key was refused: check that {keyName} in the .env file is a key for this service.",
        HttpStatusCode.NotFound =>
            "The model or the address was not found: check the Chat model and the Chat API address on the Settings page against the service's current list.",
        HttpStatusCode.TooManyRequests => "The rate limit or the free daily limit was reached: wait, or switch to another service.",
        HttpStatusCode.ServiceUnavailable => "The service is overloaded (often only for a few minutes): try again later, or use another model.",
        HttpStatusCode.BadRequest => "The request was refused; the model may not support tool calling.",
        _ => ""
    };

    private sealed class CallBuilder(int? index)
    {
        private readonly StringBuilder _arguments = new();
        private string _name = "";
        private JsonElement? _extra;

        public int? Index { get; } = index;
        public string? Id { get; private set; }

        public void Append(StreamToolCall piece)
        {
            Id ??= piece.Id;
            if (!string.IsNullOrEmpty(piece.Function?.Name))
            {
                _name = piece.Function.Name;
            }
            _arguments.Append(piece.Function?.Arguments);
            if (piece.ExtraContent is { ValueKind: JsonValueKind.Object } extra)
            {
                _extra = extra.Clone();
            }
        }

        public ModelToolCall Build()
        {
            var text = _arguments.Length == 0 ? "{}" : _arguments.ToString();
            JsonElement arguments;
            try
            {
                arguments = JsonDocument.Parse(text).RootElement.Clone();
            }
            catch (JsonException)
            {
                // Passed on as text: the tool can't read it and replies with an error the model can correct.
                arguments = JsonSerializer.SerializeToElement(text);
            }
            return new ModelToolCall(new ModelFunctionCall(_name, arguments), Id ?? NewCallId(), _extra);
        }
    }

    private sealed record StreamChunk(IReadOnlyList<StreamChoice>? Choices, StreamError? Error);

    private sealed record StreamChoice(StreamDelta? Delta);

    private sealed record StreamDelta(
        string? Content,
        [property: JsonPropertyName("tool_calls")] IReadOnlyList<StreamToolCall>? ToolCalls);

    private sealed record StreamToolCall(
        int? Index, string? Id, StreamFunction? Function,
        [property: JsonPropertyName("extra_content")] JsonElement? ExtraContent);

    private sealed record StreamFunction(string? Name, string? Arguments);

    private sealed record StreamError(string? Message);
}
