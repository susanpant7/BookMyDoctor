using BookMyDoctor.Features.Chats;
using BookMyDoctor.Features.Knowledge;
using Microsoft.Extensions.Options;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class RagPromptTests
{
    [Fact]
    public void WithKnowledge_AttachesNumberedChunksToThePatientMessage()
    {
        var knowledge = new[]
        {
            new SearchResult(new KnowledgeDocument("a", "booking.md", "Cancellation policy", "## Cancellation policy\nFree 24h ahead."), 0.7),
            new SearchResult(new KnowledgeDocument("b", "booking.md", "Refunds", "## Refunds\nBack to your card."), 0.68)
        };

        var message = OllamaChatAssistant.WithKnowledge("Can I cancel?", knowledge);

        Assert.Contains("[1] ## Cancellation policy\nFree 24h ahead.", message);
        Assert.Contains("[2] ## Refunds", message);
        Assert.EndsWith("PATIENT MESSAGE:\nCan I cancel?", message);
    }

    [Fact]
    public void WithKnowledge_SaysWhenNothingMatched()
    {
        Assert.Contains("No hospital information matched", OllamaChatAssistant.WithKnowledge("Hi", []));
    }

    [Fact]
    public void SystemPrompt_DoesNotDependOnTheQuestion()
    {
        // A stable system prompt lets Ollama reuse its cached work for it on every turn.
        var prompt = OllamaChatAssistant.SystemPrompt("Hari Bahadur", new DateOnly(2026, 10, 26));

        Assert.Contains("Hari Bahadur", prompt);
        Assert.Contains("Monday, 26 October 2026 (2026-10-26)", prompt);
        Assert.DoesNotContain("PATIENT MESSAGE", prompt);
    }

    [Fact]
    public void SystemPrompt_GivesTheModelACalendarForTheNextWeek()
    {
        var prompt = OllamaChatAssistant.SystemPrompt("Hari Bahadur", new DateOnly(2026, 10, 25));

        Assert.Contains("- tomorrow, Monday 26 October = 2026-10-26", prompt);
        Assert.Contains("- Tuesday 27 October = 2026-10-27", prompt);
        Assert.Contains("- Sunday 1 November = 2026-11-01", prompt);
    }

    [Theory]
    [InlineData(5, 0)]    // under the limit: everything
    [InlineData(12, 0)]   // at the limit: everything
    [InlineData(13, 6)]   // over: drop a block of 6
    [InlineData(18, 6)]   // same start as before, so the cached start of the prompt is reused
    [InlineData(19, 12)]  // next block
    public void RecentHistory_TrimsInBlocks(int total, int expectedStart)
    {
        var history = Enumerable.Range(0, total)
            .Select(i => new ChatMessage(Guid.NewGuid(), Guid.Empty, "patient", $"m{i}", DateTimeOffset.UnixEpoch.AddMinutes(i)))
            .ToList();

        var recent = OllamaChatAssistant.RecentHistory(history, 12);

        Assert.Equal($"m{expectedStart}", recent[0].Text);
        Assert.Equal($"m{total - 1}", recent[^1].Text);
        Assert.True(recent.Count <= 12);
    }

    [Fact]
    public async Task Retriever_DropsChunksFarBelowTheBestMatch()
    {
        var store = new InMemoryVectorStore();
        store.Upsert([
            (Doc("best"), [1, 0]),
            (Doc("close"), [0.99f, 0.14f]),   // cosine ≈ 0.990 with the query
            (Doc("weak"), [0.8f, 0.6f])       // cosine = 0.8: passes MinScore, but far below the best
        ]);
        var retriever = new KnowledgeRetriever(new FixedEmbedder([1, 0]), store,
            Options.Create(new KnowledgeOptions { Top = 4, MinScore = 0.5, MaxScoreGap = 0.05 }));

        var results = await retriever.SearchAsync("anything");

        Assert.Equal(["best", "close"], results.Select(result => result.Document.Id));
    }

    private static KnowledgeDocument Doc(string id) => new(id, "test.md", id, $"## {id}");

    private sealed class FixedEmbedder(float[] vector) : IEmbedder
    {
        public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => vector).ToList());

        public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(vector);
    }
}
