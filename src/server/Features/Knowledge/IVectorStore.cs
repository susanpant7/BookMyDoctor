namespace BookMyDoctor.Features.Knowledge;

public sealed record SearchResult(KnowledgeDocument Document, double Score);

// Stores documents with their embedding vectors and finds the ones closest in meaning to a query vector.
public interface IVectorStore
{
    int Count { get; }

    // Adds documents, replacing any existing document with the same Id.
    void Upsert(IReadOnlyList<(KnowledgeDocument Document, float[] Vector)> items);

    // Removes every document of the source and adds the given ones, in one transaction.
    void ReplaceSource(string source, IReadOnlyList<(KnowledgeDocument Document, float[] Vector)> items);

    void Remove(IReadOnlyCollection<string> ids);

    // Returns how many documents were removed.
    int RemoveSource(string source);

    // Content hash of each stored document of the source, by id, to find the ones that changed.
    IReadOnlyDictionary<string, string> GetHashes(string source);

    IReadOnlyList<KnowledgeDocument> GetAll();

    // Most similar documents first, at most `top`, only those scoring at least `minScore` (cosine similarity, -1 to 1).
    // Throws InvalidOperationException when the stored vectors cannot be compared with the query (another embedding model).
    IReadOnlyList<SearchResult> Search(float[] queryVector, int top = 4, double minScore = 0.5);
}
