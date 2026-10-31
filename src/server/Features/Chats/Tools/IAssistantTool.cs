using System.Text.Json;

namespace BookMyDoctor.Features.Chats.Tools;

// Who the tool is running for (from the signed-in patient's token, never from the model)
// and which patient message this turn is answering.
public sealed record ToolContext(Guid PatientId, Guid MessageId = default)
{
    // The free times looked up while answering this message (the last lookup wins), shown as buttons after the reply.
    public SlotOffer? SlotsShown { get; set; }
}

// A function the AI model can ask the server to run. The model only sees Name, Description and Parameters;
// the server decides whether and how to run it.
public interface IAssistantTool
{
    string Name { get; }

    // Tells the model what the tool returns and when to use it. This is how the model chooses tools.
    string Description { get; }

    // JSON Schema of the arguments the model should fill in.
    JsonElement Parameters { get; }

    // Returns a short plain-text result for the model to read. Problems with the arguments should be
    // returned as text too (e.g. "No doctor named …") so the model can correct itself.
    Task<string> RunAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken);
}
