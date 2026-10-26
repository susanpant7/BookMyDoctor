namespace BookMyDoctor.Features.Knowledge;

// Brute-force search: compares the query with every stored vector. Fast enough for a few thousand documents.
public sealed class InMemoryVectorStore : IVectorStore
{
    private sealed record Entry(KnowledgeDocument Document, float[] UnitVector);

    // Replaced as a whole on every write, so searches running at the same time always see a complete list.
    private IReadOnlyList<Entry> _entries = [];
    private readonly Lock _writeLock = new();

    public int Count => _entries.Count;

    public void Upsert(IReadOnlyList<(KnowledgeDocument Document, float[] Vector)> items)
    {
        lock (_writeLock)
        {
            var byId = _entries.ToDictionary(entry => entry.Document.Id);
            foreach (var (document, vector) in items)
            {
                var dimensions = byId.Values.FirstOrDefault()?.UnitVector.Length ?? vector.Length;
                if (vector.Length != dimensions)
                {
                    throw new ArgumentException($"Vector for '{document.Id}' has {vector.Length} dimensions; the store uses {dimensions}.");
                }
                byId[document.Id] = new Entry(document, Normalize(vector));
            }
            _entries = [.. byId.Values];
        }
    }

    public IReadOnlyList<SearchResult> Search(float[] queryVector, int top = 4, double minScore = 0.5)
    {
        var query = Normalize(queryVector);
        return _entries
            .Select(entry => new SearchResult(entry.Document, Dot(query, entry.UnitVector)))
            .Where(result => result.Score >= minScore)
            .OrderByDescending(result => result.Score)
            .Take(top)
            .ToList();
    }

    // Vectors are scaled to length 1 when stored, so cosine similarity becomes a plain dot product at search time.
    private static float[] Normalize(float[] vector)
    {
        var length = Math.Sqrt(vector.Sum(value => (double)value * value));
        return length == 0 ? vector : vector.Select(value => (float)(value / length)).ToArray();
    }

    private static double Dot(float[] left, float[] right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException($"Query has {left.Length} dimensions; the store uses {right.Length}.");
        }

        double sum = 0;
        for (var i = 0; i < left.Length; i++)
        {
            sum += left[i] * right[i];
        }
        return sum;
    }
}
