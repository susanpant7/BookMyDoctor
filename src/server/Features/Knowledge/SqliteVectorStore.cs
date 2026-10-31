using System.Runtime.InteropServices;
using BookMyDoctor.Persistence;
using BookMyDoctor.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookMyDoctor.Features.Knowledge;

// Keeps the documents and their vectors in the KnowledgeChunks table and searches them with sqlite-vec.
public sealed class SqliteVectorStore : IVectorStore
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SqliteVectorStore(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public int Count
    {
        get
        {
            using var db = _factory.CreateDbContext();
            return db.KnowledgeChunks.Count();
        }
    }

    public void Upsert(IReadOnlyList<(KnowledgeDocument Document, float[] Vector)> items)
    {
        using var db = _factory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        Write(db, items);
        transaction.Commit();
    }

    public void ReplaceSource(string source, IReadOnlyList<(KnowledgeDocument Document, float[] Vector)> items)
    {
        using var db = _factory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        db.KnowledgeChunks.Where(chunk => chunk.Source == source).ExecuteDelete();
        Write(db, items);
        transaction.Commit();
    }

    public void Remove(IReadOnlyCollection<string> ids)
    {
        using var db = _factory.CreateDbContext();
        db.KnowledgeChunks.Where(chunk => ids.Contains(chunk.Id)).ExecuteDelete();
    }

    public int RemoveSource(string source)
    {
        using var db = _factory.CreateDbContext();
        return db.KnowledgeChunks.Where(chunk => chunk.Source == source).ExecuteDelete();
    }

    public IReadOnlyDictionary<string, string> GetHashes(string source)
    {
        using var db = _factory.CreateDbContext();
        return db.KnowledgeChunks.Where(chunk => chunk.Source == source).ToDictionary(chunk => chunk.Id, chunk => chunk.ContentHash);
    }

    public IReadOnlyList<KnowledgeDocument> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.KnowledgeChunks
            .AsNoTracking()
            .OrderBy(chunk => chunk.Source).ThenBy(chunk => chunk.IngestedAt).ThenBy(chunk => chunk.Id)
            .Select(chunk => new KnowledgeDocument(chunk.Id, chunk.Source, chunk.Title, chunk.Text))
            .ToList();
    }

    public IReadOnlyList<SearchResult> Search(float[] queryVector, int top = 4, double minScore = 0.5)
    {
        using var db = _factory.CreateDbContext();
        var query = MemoryMarshal.AsBytes(queryVector.AsSpan()).ToArray();
        try
        {
            // Raw SQL because EF Core's LINQ does not know sqlite-vec's functions.
            return db.Database.SqlQuery<SearchRow>($"""
                    SELECT Id, Source, Title, Text, vec_distance_cosine(Embedding, {query}) AS Distance
                    FROM KnowledgeChunks
                    ORDER BY Distance
                    LIMIT {top}
                    """)
                .ToList()
                .Select(row => new SearchResult(new KnowledgeDocument(row.Id, row.Source, row.Title, row.Text), 1 - row.Distance))
                .Where(result => result.Score >= minScore)
                .ToList();
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException(
                "The stored knowledge cannot be searched with this embedding model. Upload the knowledge files again.", ex);
        }
    }

    private static void Write(AppDbContext db, IReadOnlyList<(KnowledgeDocument Document, float[] Vector)> items)
    {
        var ids = items.Select(item => item.Document.Id).ToList();
        db.KnowledgeChunks.Where(chunk => ids.Contains(chunk.Id)).ExecuteDelete();

        var now = DateTime.UtcNow;
        db.KnowledgeChunks.AddRange(items.Select(item => new KnowledgeChunkEntity
        {
            Id = item.Document.Id,
            Source = item.Document.Source,
            Title = item.Document.Title,
            Text = item.Document.Text,
            ContentHash = item.Document.Hash,
            Embedding = MemoryMarshal.AsBytes(item.Vector.AsSpan()).ToArray(),
            IngestedAt = now
        }));
        db.SaveChanges();
    }

    // Shape of the raw SQL search query: a document plus its cosine distance to the query vector.
    private sealed class SearchRow
    {
        public string Id { get; set; } = "";
        public string Source { get; set; } = "";
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public double Distance { get; set; }
    }
}
