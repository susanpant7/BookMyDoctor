using BookMyDoctor.Features.Knowledge;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class InMemoryVectorStoreTests
{
    // Tiny 3-number "embeddings" so the expected similarities are easy to work out by hand.
    private static readonly KnowledgeDocument Refunds = Doc("refunds");
    private static readonly KnowledgeDocument Rashes = Doc("rashes");
    private static readonly KnowledgeDocument Parking = Doc("parking");

    [Fact]
    public void Search_ReturnsMostSimilarFirst()
    {
        var store = Store();

        var results = store.Search([1, 0.1f, 0], top: 3, minScore: -1);

        Assert.Equal(["refunds", "rashes", "parking"], results.Select(result => result.Document.Id));
        Assert.True(results[0].Score > 0.99);
    }

    [Fact]
    public void Search_IgnoresVectorLength()
    {
        var store = Store();

        // Same direction as "refunds" but 10x longer: cosine similarity only cares about direction (meaning).
        var results = store.Search([10, 0, 0], top: 1);

        Assert.Equal("refunds", results.Single().Document.Id);
        Assert.Equal(1, results.Single().Score, precision: 5);
    }

    [Fact]
    public void Search_DropsResultsBelowMinScoreAndLimitsToTop()
    {
        var store = Store();

        Assert.Single(store.Search([1, 0, 0], top: 3, minScore: 0.9));
        Assert.Equal(2, store.Search([1, 0.1f, 0], top: 2, minScore: -1).Count);
    }

    [Fact]
    public void Upsert_ReplacesDocumentWithSameId()
    {
        var store = Store();

        store.Upsert([(Refunds with { Text = "updated" }, [0, 0, 1])]);

        Assert.Equal(3, store.Count);
        var top = store.Search([0, 0, 1], top: 1).Single();
        Assert.Equal("refunds", top.Document.Id);
        Assert.Equal("updated", top.Document.Text);
    }

    [Fact]
    public void Upsert_RejectsVectorsOfADifferentSize()
    {
        var store = Store();

        Assert.Throws<ArgumentException>(() => store.Upsert([(Doc("other"), [1, 0])]));
    }

    private static InMemoryVectorStore Store()
    {
        var store = new InMemoryVectorStore();
        store.Upsert([(Refunds, [1, 0, 0]), (Rashes, [0.7f, 0.7f, 0]), (Parking, [0, 0, 1])]);
        return store;
    }

    private static KnowledgeDocument Doc(string id) => new(id, "test.md", id, $"## {id}");
}
