using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlogSpecLab.Core.Application.EditorialKnowledge;

namespace BlogSpecLab.Infra.AI.EditorialKnowledge;

/// <summary>
/// Adapter سادهٔ REST برای Qdrant 1.19؛ در این گام تنها Create/Get Collection و Upsert لازم است.
/// Retrieval و حذف نسخه‌های قدیمی در مراحل بعدی طراحی می‌شوند.
/// </summary>
public sealed class QdrantEditorialKnowledgeStore : IEditorialKnowledgeStore
{
    private readonly HttpClient http;
    private readonly string collectionPath;

    public QdrantEditorialKnowledgeStore(HttpClient http, string collectionName)
    {
        this.http = http;
        if (http.BaseAddress is null ||
            !Regex.IsMatch(collectionName, "^[a-zA-Z0-9_-]{3,64}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("آدرس Qdrant یا نام Collection نامعتبر است.");
        collectionPath = "collections/" + collectionName;
    }

    public async Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken)
    {
        if (dimensions is <= 0 or > 8192)
            throw new ArgumentOutOfRangeException(nameof(dimensions));

        using var current = await http.GetAsync(collectionPath, cancellationToken);
        if (current.StatusCode == HttpStatusCode.NotFound)
        {
            using var created = await http.PutAsJsonAsync(collectionPath,
                new { vectors = new { size = dimensions, distance = "Cosine" } }, cancellationToken);
            using var creation = await ReadSuccessfulAsync(created, cancellationToken);
            return;
        }

        using var json = await ReadSuccessfulAsync(current, cancellationToken);
        var vectors = json.RootElement.GetProperty("result").GetProperty("config")
            .GetProperty("params").GetProperty("vectors");
        // Collection قدیمی با ابعاد یا معیار متفاوت نباید به‌صورت ضمنی بازاستفاده شود.
        if (vectors.GetProperty("size").GetInt32() != dimensions ||
            vectors.GetProperty("distance").GetString() != "Cosine")
            throw new InvalidOperationException("Collection موجود با قرارداد بردارهای این Feature ناسازگار است.");
    }

    public async Task UpsertAsync(IReadOnlyList<EditorialKnowledgeVector> points, CancellationToken cancellationToken)
    {
        if (points.Count is < 1 or > 64 || points.Select(x => x.EmbeddingModel).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new ArgumentException("دادهٔ Upsert یا مدل Embedding ناسازگار است.", nameof(points));

        var data = new
        {
            points = points.Select(point => new
            {
                id = point.Chunk.Id.ToString("D"),
                vector = point.Vector,
                payload = new
                {
                    documentId = point.Chunk.DocumentId,
                    version = point.Chunk.Version,
                    title = point.Chunk.Title,
                    section = point.Chunk.Section,
                    content = point.Chunk.Content,
                    contentSha256 = point.Chunk.ContentSha256,
                    embeddingModel = point.EmbeddingModel,
                    sourceKind = "training_editorial",
                    accessScope = "editorial_demo"
                }
            }).ToArray()
        };
        using var response = await http.PutAsJsonAsync(collectionPath + "/points?wait=true", data, cancellationToken);
        using var json = await ReadSuccessfulAsync(response, cancellationToken);
        if (!json.RootElement.TryGetProperty("result", out var result) ||
            result.GetProperty("status").GetString() != "completed")
            throw new InvalidOperationException("پایگاه برداری اتمام ثبت Pointها را تأیید نکرد.");
    }

    private static async Task<JsonDocument> ReadSuccessfulAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"عملیات Qdrant ناموفق بود؛ HTTP {(int)response.StatusCode}.");
        // از ثبت یا نمایش متن خام پاسخ سرویس، URL خصوصی و Credential جلوگیری می‌کنیم.
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("status", out var status) || status.GetString() != "ok")
        {
            json.Dispose();
            throw new InvalidOperationException("پاسخ Qdrant معتبر یا موفق نبود.");
        }
        return json;
    }
}
