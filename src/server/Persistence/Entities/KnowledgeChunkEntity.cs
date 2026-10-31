namespace BookMyDoctor.Persistence.Entities;

/// <summary>One section of a knowledge file (or a doctor profile), with its embedding for similarity search.</summary>
public class KnowledgeChunkEntity
{
    /// <summary>Stable id, for example "booking.md#how-to-book" or "doctor:2222…".</summary>
    public required string Id { get; set; }

    /// <summary>The file name, or "doctors" for doctor profiles.</summary>
    public required string Source { get; set; }

    public required string Title { get; set; }
    public required string Text { get; set; }

    /// <summary>SHA-256 of the text, used to skip chunks that have not changed.</summary>
    public required string ContentHash { get; set; }

    /// <summary>The embedding as raw little-endian 32-bit floats (the format sqlite-vec expects).</summary>
    public required byte[] Embedding { get; set; }

    public DateTime IngestedAt { get; set; }
}
