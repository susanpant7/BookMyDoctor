using System.Text.Json.Serialization;

namespace MyHospitalAI.Features.Chats;

public sealed record OllamaChatRequest(
    string Model,
    IReadOnlyList<ModelMessage> Messages,
    bool Stream,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ModelTool>? Tools = null,
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
    ModelMessage? Message,
    bool Done,
    string? Error = null,
    [property: JsonPropertyName("load_duration")] long LoadDuration = 0,
    [property: JsonPropertyName("prompt_eval_count")] int PromptEvalCount = 0,
    [property: JsonPropertyName("prompt_eval_duration")] long PromptEvalDuration = 0,
    [property: JsonPropertyName("eval_count")] int EvalCount = 0,
    [property: JsonPropertyName("eval_duration")] long EvalDuration = 0);

public sealed record OllamaEmbedRequest(string Model, IReadOnlyList<string> Input);

public sealed record OllamaEmbedResponse(float[][] Embeddings);
