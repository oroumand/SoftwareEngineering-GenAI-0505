using System.Net;
using System.Text;
using System.Text.Json;
using BlogSpecLab.Core.Application.EditorialKnowledge;
using BlogSpecLab.Infra.AI.EditorialKnowledge;
using Xunit;

namespace BlogSpecLab.Tests.AI;

public class QdrantEditorialKnowledgeStoreTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public readonly List<(string Method, string Path, string Body)> Calls = [];
        public bool Existing;
        public int Dimensions = 3;
        public string Distance = "Cosine";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var path = request.RequestUri!.PathAndQuery;
            Calls.Add((request.Method.Method, path, body));
            if (request.Method == HttpMethod.Get && !Existing)
                return new(HttpStatusCode.NotFound);
            var result = request.Method == HttpMethod.Get
                ? JsonSerializer.Serialize(new
                {
                    status = "ok",
                    result = new { config = new { @params = new { vectors = new { size = Dimensions, distance = Distance } } } }
                })
                : path.Contains("/points", StringComparison.Ordinal)
                    ? """{"status":"ok","result":{"status":"completed"}}"""
                    : """{"status":"ok","result":true}""";
            return new(HttpStatusCode.OK) { Content = new StringContent(result, Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task Creates_collection_and_upserts_idempotent_ids_with_metadata()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new("http://localhost:6333/") };
        var store = new QdrantEditorialKnowledgeStore(http, "blogspec_editorial");
        await store.EnsureCollectionAsync(3, TestContext.Current.CancellationToken);
        var chunk = Assert.Single(MarkdownSectionChunker.Split(new("editorial-policy", "1.0.0",
            "سیاست", "## آمار\nادعای عددی منبع می‌خواهد.")));
        await store.UpsertAsync([new(chunk, "fake-embedding", [0.2f, 0.4f, 0.8f])],
            TestContext.Current.CancellationToken);
        Assert.Equal(3, handler.Calls.Count);
        using var create = JsonDocument.Parse(handler.Calls[1].Body);
        Assert.Equal(3, create.RootElement.GetProperty("vectors").GetProperty("size").GetInt32());
        using var upsert = JsonDocument.Parse(handler.Calls[2].Body);
        var point = upsert.RootElement.GetProperty("points")[0];
        Assert.Equal(chunk.Id.ToString("D"), point.GetProperty("id").GetString());
        Assert.Equal("1.0.0", point.GetProperty("payload").GetProperty("version").GetString());
        Assert.Equal(chunk.ContentSha256, point.GetProperty("payload").GetProperty("contentSha256").GetString());
        Assert.Equal("editorial_demo", point.GetProperty("payload").GetProperty("accessScope").GetString());
    }

    [Fact]
    public async Task Existing_collection_with_wrong_vector_config_is_rejected()
    {
        using var handler = new Handler { Existing = true, Distance = "Dot" };
        using var http = new HttpClient(handler) { BaseAddress = new("http://localhost:6333/") };
        var store = new QdrantEditorialKnowledgeStore(http, "blogspec_editorial");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.EnsureCollectionAsync(3, TestContext.Current.CancellationToken));
        Assert.Single(handler.Calls);
    }
}
