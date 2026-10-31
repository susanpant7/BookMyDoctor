using System.Security.Cryptography;
using System.Text;

namespace BookMyDoctor.Features.Knowledge;

// One self-contained piece of text that can be embedded and retrieved for RAG.
public sealed record KnowledgeDocument(
    string Id,      // stable id, e.g. "booking.md#rescheduling-an-appointment" or "doctor:2222…"
    string Source,  // where it came from, e.g. "booking.md" or "doctors"
    string Title,   // the section heading
    string Text)    // what gets embedded and later shown to the model
{
    public string Hash => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Text)));
}
