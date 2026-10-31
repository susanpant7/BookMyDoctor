using System.Security.Cryptography;
using System.Text;

namespace MyHospitalAI.Features.Knowledge;

// One self-contained piece of text that can be embedded and retrieved for RAG.
public sealed record KnowledgeDocument(
    string Id,      // stable id, e.g. "booking.md#rescheduling-an-appointment" or "doctor-settings.md#2222…"
    string Source,  // where it came from, e.g. "booking.md" or "doctor-settings.md"
    string Title,   // the section heading
    string Text,    // what gets embedded and later shown to the model
    string DocumentTitle = "",   // the whole file's title, e.g. "Cancellations and refunds"
    int Position = 0)            // where the section is in the file
{
    public string Hash => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Text)));
}
