using BlogSpecLab.Core.Application.EditorialKnowledge;
using Xunit;

namespace BlogSpecLab.Tests.Application;

public class EditorialKnowledgeIngestionTests
{
    private sealed class Embeddings : IEditorialEmbeddingGenerator
    {
        public string ModelId => "fake-embedding-model";
        public int Calls;
        public bool FailSecond;
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(FailSecond && Calls == 2 ? new[] { float.NaN, 0f, 1f } :
                new[] { 0.2f, 0.4f, 0.8f });
        }
    }

    private sealed class Store : IEditorialKnowledgeStore
    {
        public int Dimensions;
        public int Ensures;
        public IReadOnlyList<EditorialKnowledgeVector>? Points;
        public Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken)
        {
            Ensures++;
            Dimensions = dimensions;
            return Task.CompletedTask;
        }
        public Task UpsertAsync(IReadOnlyList<EditorialKnowledgeVector> points, CancellationToken cancellationToken)
        {
            Points = points;
            return Task.CompletedTask;
        }
    }

    private static EditorialKnowledgeSource Source => new("editorial-policy", "1.0.0",
        "سیاست", "## عنوان\nعنوان روشن باشد.\n## آمار\nآمار منبع داشته باشد.");

    [Fact]
    public async Task Prepares_every_embedding_then_upserts_a_single_consistent_batch()
    {
        var embedding = new Embeddings();
        var store = new Store();
        var ingestion = new EditorialKnowledgeIngestionService(embedding, store);
        Assert.Equal(2, await ingestion.IngestAsync(Source, TestContext.Current.CancellationToken));
        Assert.Equal(2, embedding.Calls);
        Assert.Equal(1, store.Ensures);
        Assert.Equal(3, store.Dimensions);
        Assert.NotNull(store.Points);
        Assert.Equal(2, store.Points.Count);
        Assert.All(store.Points, p => Assert.Equal(embedding.ModelId, p.EmbeddingModel));
        Assert.Equal(MarkdownSectionChunker.Split(Source).Select(p => p.Id), store.Points.Select(p => p.Chunk.Id));
    }

    [Fact]
    public async Task Invalid_embedding_does_not_write_any_points()
    {
        var embedding = new Embeddings { FailSecond = true };
        var store = new Store();
        var ingestion = new EditorialKnowledgeIngestionService(embedding, store);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ingestion.IngestAsync(Source, TestContext.Current.CancellationToken));
        Assert.Equal(0, store.Ensures);
        Assert.Null(store.Points);
    }
}
