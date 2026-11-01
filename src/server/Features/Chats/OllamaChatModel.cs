using System.Text;
using System.Text.Json;
using MyHospitalAI.Features.Settings;

namespace MyHospitalAI.Features.Chats;

// A model run by Ollama, called with Ollama's own API at the "Chat API address" setting.
public sealed class OllamaChatModel : IChatModel
{
    private readonly HttpClient _http;
    private readonly ILogger<OllamaChatModel> _logger;

    public OllamaChatModel(HttpClient http, ILogger<OllamaChatModel> logger)
    {
        _http = http;
        _logger = logger;
    }

    // Streams the model's answer: Ollama sends one JSON object per line, each with the next piece of text, and a final
    // line with the tool calls (if any) and timings.
    public async Task<ModelMessage> ChatAsync(
        IReadOnlyList<ModelMessage> messages, IReadOnlyList<ModelTool> tools, AiSettings settings, Func<string, Task>? onText)
    {
        var request = new OllamaChatRequest(
            settings.ChatModel, messages, Stream: true, tools.Count == 0 ? null : tools,
            new OllamaRequestOptions(settings.ContextSize, settings.Temperature), settings.KeepAlive);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{settings.ChatBaseUrl}/api/chat") { Content = JsonContent.Create(request) };
        // ResponseHeadersRead: start reading as soon as Ollama starts sending, instead of waiting for the whole reply.
        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Ollama returned {Status} for model {Model}: {Detail}. If the model is missing, run: ollama pull {Model}",
                (int)response.StatusCode, settings.ChatModel, detail, settings.ChatModel);
            throw new HttpRequestException($"Ollama returned {(int)response.StatusCode}.", null, response.StatusCode);
        }

        var content = new StringBuilder();
        var toolCalls = new List<ModelToolCall>();
        OllamaChatResponse? last = null;
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) || JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonSerializerOptions.Web) is not { } chunk)
            {
                continue;
            }

            if (chunk.Error is { } error)
            {
                _logger.LogWarning("Ollama failed while answering: {Error}", error);
                throw new HttpRequestException($"Ollama failed while answering: {error}");
            }

            last = chunk;
            if (chunk.Message?.ToolCalls is { Count: > 0 } calls)
            {
                toolCalls.AddRange(calls);
            }

            if (!string.IsNullOrEmpty(chunk.Message?.Content))
            {
                content.Append(chunk.Message.Content);
                if (onText is not null && toolCalls.Count == 0)
                {
                    await onText(content.ToString());
                }
            }
        }

        if (last is not null)
        {
            _logger.LogInformation(
                "Ollama call: {Messages} messages | load {Load:F1}s | prompt {PromptTokens} tokens in {PromptSeconds:F1}s | reply {ReplyTokens} tokens in {ReplySeconds:F1}s",
                messages.Count, last.LoadDuration / 1e9, last.PromptEvalCount, last.PromptEvalDuration / 1e9,
                last.EvalCount, last.EvalDuration / 1e9);
        }
        return new ModelMessage("assistant", content.ToString(), toolCalls.Count > 0 ? toolCalls : null);
    }
}
