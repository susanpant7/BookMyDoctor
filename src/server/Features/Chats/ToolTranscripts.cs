using System.Collections.Concurrent;

namespace BookMyDoctor.Features.Chats;

// The tool calls and results from each patient's recent turns. The chat history only stores what the patient
// sees, so without this the model would forget the looked-up facts (doctors, fees, times) by the next message.
public sealed class ToolTranscripts
{
    private sealed record Turn(Guid PatientMessageId, IReadOnlyList<OllamaMessage> Messages);

    private readonly ConcurrentDictionary<Guid, List<Turn>> _byPatient = new();

    // Remembers the tool messages produced while answering one patient message, keeping only the last `keepTurns`.
    public void Save(Guid patientId, Guid patientMessageId, IReadOnlyList<OllamaMessage> toolMessages, int keepTurns)
    {
        if (toolMessages.Count == 0)
        {
            return;
        }

        var turns = _byPatient.GetOrAdd(patientId, _ => []);
        lock (turns)
        {
            turns.Add(new Turn(patientMessageId, toolMessages));
            if (turns.Count > keepTurns)
            {
                turns.RemoveRange(0, turns.Count - keepTurns);
            }
        }
    }

    // The tool messages to replay right after the given patient message, or none.
    public IReadOnlyList<OllamaMessage> For(Guid patientId, Guid patientMessageId)
    {
        if (!_byPatient.TryGetValue(patientId, out var turns))
        {
            return [];
        }

        lock (turns)
        {
            return turns.FirstOrDefault(turn => turn.PatientMessageId == patientMessageId)?.Messages ?? [];
        }
    }
}
