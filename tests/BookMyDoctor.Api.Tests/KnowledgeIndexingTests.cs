using BookMyDoctor.Features.Knowledge;
using BookMyDoctor.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class KnowledgeIndexingTests
{
    [Fact]
    public async Task Indexer_StoresEveryDocument()
    {
        var sources = new KnowledgeSources(new DoctorsRepo());
        var store = new InMemoryVectorStore();
        var indexer = new KnowledgeIndexer(sources, new KeywordEmbedder(), store, NullLogger<KnowledgeIndexer>.Instance);

        await indexer.IndexAsync(CancellationToken.None);

        Assert.Equal(sources.GetDocuments().Count, store.Count);
    }

    [Fact]
    public async Task Retriever_FindsMatchingChunkAfterIndexing()
    {
        var store = new InMemoryVectorStore();
        var embedder = new KeywordEmbedder();
        var indexer = new KnowledgeIndexer(new KnowledgeSources(new DoctorsRepo()), embedder, store, NullLogger<KnowledgeIndexer>.Instance);
        await indexer.IndexAsync(CancellationToken.None);

        var results = await Retriever(embedder, store).SearchAsync("refund");

        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.Contains("refund", result.Document.Text, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Retriever_ReturnsNothingBeforeIndexingWithoutCallingTheEmbedder()
    {
        var embedder = new KeywordEmbedder();

        var results = await Retriever(embedder, new InMemoryVectorStore()).SearchAsync("refund");

        Assert.Empty(results);
        Assert.Equal(0, embedder.QueryCalls);
    }

    private static KnowledgeRetriever Retriever(IEmbedder embedder, IVectorStore store) =>
        new(embedder, store, Options.Create(new KnowledgeOptions { Top = 4, MinScore = 0.5 }));

    // A stand-in for a real embedding model: one dimension per keyword, 1 if the text mentions it.
    private sealed class KeywordEmbedder : IEmbedder
    {
        private static readonly string[] Keywords = ["refund", "rash", "parking", "insurance", "child"];

        public int QueryCalls { get; private set; }

        public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(texts.Select(Embed).ToList());

        public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default)
        {
            QueryCalls++;
            return Task.FromResult(Embed(text));
        }

        private static float[] Embed(string text) =>
            Keywords.Select(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase) ? 1f : 0f).ToArray();
    }
}
