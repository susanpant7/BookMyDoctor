namespace BookMyDoctor.Features.Knowledge;

// Turns text into vectors whose distance reflects meaning.
// Documents and queries are embedded separately because some models (like nomic-embed-text) treat them differently.
public interface IEmbedder
{
    Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
    Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default);
}
