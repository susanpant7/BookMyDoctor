namespace BookMyDoctor.Features.Knowledge;

public sealed record SearchResult(KnowledgeDocument Document, double Score);

// Stores documents with their embedding vectors and finds the ones closest in meaning to a query vector.
// In-memory for now; a pgvector-backed version can replace it without changing callers.
public interface IVectorStore
{
    int Count { get; }

    // Adds documents, replacing any existing document with the same Id.
    void Upsert(IReadOnlyList<(KnowledgeDocument Document, float[] Vector)> items);

    // Most similar documents first, at most `top`, only those scoring at least `minScore` (cosine similarity, -1 to 1).
    IReadOnlyList<SearchResult> Search(float[] queryVector, int top = 4, double minScore = 0.5);
}
