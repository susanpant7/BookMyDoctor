using MyHospitalAI.Features.Settings;

namespace MyHospitalAI.Features.Knowledge;

// Turns text into vectors whose distance reflects meaning.
// Documents and queries are embedded separately because some models (like nomic-embed-text) treat them differently.
public interface IEmbedder
{
    Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);

    // Embeds with the given embedding settings instead of the saved ones, e.g. to try a model before switching to it.
    Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, AiSettings settings, CancellationToken cancellationToken = default);
    Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default);
}
