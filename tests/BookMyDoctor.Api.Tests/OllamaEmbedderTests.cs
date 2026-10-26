using System.Net.Http.Json;
using BookMyDoctor.Features.Chats;
using BookMyDoctor.Features.Knowledge;
using Microsoft.Extensions.Options;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class OllamaEmbedderTests
{
    [Fact]
    public async Task EmbedDocuments_SendsBatchesWithDocumentPrefix()
    {
        var handler = new FakeOllama();
        var texts = Enumerable.Range(1, 70).Select(i => $"chunk {i}").ToList();

        var vectors = await Embedder(handler).EmbedDocumentsAsync(texts);

        Assert.Equal(70, vectors.Count);
        Assert.Equal([32, 32, 6], handler.Requests.Select(request => request.Input.Count));
        Assert.All(handler.Requests, request => Assert.Equal("nomic-embed-text", request.Model));
        Assert.Equal("search_document: chunk 1", handler.Requests[0].Input[0]);
        Assert.Equal("search_document: chunk 70", handler.Requests[2].Input[^1]);
    }

    [Fact]
    public async Task EmbedQuery_UsesQueryPrefix()
    {
        var handler = new FakeOllama();

        var vector = await Embedder(handler).EmbedQueryAsync("Can I cancel?");

        Assert.Equal(3, vector.Length);
        Assert.Equal(["search_query: Can I cancel?"], handler.Requests.Single().Input);
    }

    private static OllamaEmbedder Embedder(FakeOllama handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://ollama.test") },
            Options.Create(new OllamaOptions { EmbeddingModel = "nomic-embed-text" }));

    // Records each /api/embed request and answers with a tiny 3-number vector per input.
    private sealed class FakeOllama : HttpMessageHandler
    {
        public List<OllamaEmbedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadFromJsonAsync<OllamaEmbedRequest>(cancellationToken);
            Requests.Add(body!);
            var embeddings = body!.Input.Select((_, i) => new[] { i, 0.5f, 1f }).ToArray();
            return new HttpResponseMessage { Content = JsonContent.Create(new OllamaEmbedResponse(embeddings)) };
        }
    }
}
