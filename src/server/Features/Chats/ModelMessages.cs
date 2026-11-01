using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyHospitalAI.Features.Chats;

// The conversation as the assistant builds it, the same for every provider. Each chat model client turns it into
// its service's format. The JSON names are Ollama's, so the Ollama client can send these as they are.
public sealed record ModelMessage(
    string Role,
    string Content,
    [property: JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ModelToolCall>? ToolCalls = null,
    // On a "tool" message: which tool replied (Ollama), and which call it answers (online providers).
    [property: JsonPropertyName("tool_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ToolName = null,
    [property: JsonPropertyName("tool_call_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ToolCallId = null);

// A request from the model to run one of our tools, with the arguments it filled in. Online providers give each
// call an id, which the tool's result must repeat. Extra is anything else the provider attached to the call and
// expects back unchanged with the next request (Gemini's thought_signature, in "extra_content").
public sealed record ModelToolCall(
    ModelFunctionCall Function,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Id = null,
    [property: JsonIgnore] JsonElement? Extra = null);

public sealed record ModelFunctionCall(string Name, JsonElement Arguments);

// A tool definition sent to the model: what it's called, when to use it, and its parameters (a JSON Schema).
// The same shape for Ollama and the online providers.
public sealed record ModelTool(string Type, ModelFunction Function);

public sealed record ModelFunction(string Name, string Description, JsonElement Parameters);
