using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookMyDoctor.Features.Chats;

// Ollama uses snake_case for tool fields, so those properties are named explicitly.
public sealed record OllamaMessage(
    string Role,
    string Content,
    [property: JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<OllamaToolCall>? ToolCalls = null,
    [property: JsonPropertyName("tool_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ToolName = null);

// A request from the model to run one of our tools, with the arguments it filled in.
public sealed record OllamaToolCall(OllamaFunctionCall Function);

public sealed record OllamaFunctionCall(string Name, JsonElement Arguments);

// A tool definition sent to the model: what it's called, when to use it, and its parameters (a JSON Schema).
public sealed record OllamaTool(string Type, OllamaFunction Function);

public sealed record OllamaFunction(string Name, string Description, JsonElement Parameters);

public sealed record OllamaChatRequest(
    string Model,
    IReadOnlyList<OllamaMessage> Messages,
    bool Stream,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<OllamaTool>? Tools = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OllamaRequestOptions? Options = null,
    [property: JsonPropertyName("keep_alive"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? KeepAlive = null);

public sealed record OllamaRequestOptions(
    [property: JsonPropertyName("num_ctx")] int NumCtx,
    [property: JsonPropertyName("temperature")] double Temperature);

// Durations are in nanoseconds. PromptEvalCount is how many prompt tokens had to be (re)processed;
// tokens Ollama could reuse from its cache of the previous request are not counted.
public sealed record OllamaChatResponse(
    OllamaMessage Message,
    bool Done,
    [property: JsonPropertyName("load_duration")] long LoadDuration = 0,
    [property: JsonPropertyName("prompt_eval_count")] int PromptEvalCount = 0,
    [property: JsonPropertyName("prompt_eval_duration")] long PromptEvalDuration = 0,
    [property: JsonPropertyName("eval_count")] int EvalCount = 0,
    [property: JsonPropertyName("eval_duration")] long EvalDuration = 0);

public sealed record OllamaEmbedRequest(string Model, IReadOnlyList<string> Input);

public sealed record OllamaEmbedResponse(float[][] Embeddings);
