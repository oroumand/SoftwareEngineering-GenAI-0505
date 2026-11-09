namespace BlogSpecLab.Core.Application.EditorialKnowledge;

/// <summary>
/// Ingestion مستقل از درخواست HTTP نویسنده؛ فقط منبع تأییدشده را با ابزار اپراتوری ایندکس می‌کند.
/// </summary>
public sealed class EditorialKnowledgeIngestionService(
    IEditorialEmbeddingGenerator embeddings,
    IEditorialKnowledgeStore store)
{
    public async Task<int> IngestAsync(EditorialKnowledgeSource source, CancellationToken cancellationToken)
    {
        var chunks = MarkdownSectionChunker.Split(source);
        var vectors = new List<EditorialKnowledgeVector>(chunks.Count);
        int? dimensions = null;

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vector = await embeddings.EmbedAsync(chunk.Section + "\n" + chunk.Content, cancellationToken);
            if (vector is null || vector.Length == 0 ||
                vector.Any(value => !float.IsFinite(value)) ||
                vector.All(value => value == 0) ||
                (dimensions is not null && vector.Length != dimensions))
                throw new InvalidOperationException("Embedding خالی، نامعتبر یا دارای ابعاد ناسازگار است.");
            dimensions ??= vector.Length;
            vectors.Add(new(chunk, embeddings.ModelId, vector));
        }

        // تا پیش از آماده شدن همهٔ Embeddingها هیچ Pointی نوشته نمی‌شود.
        await store.EnsureCollectionAsync(dimensions!.Value, cancellationToken);
        await store.UpsertAsync(vectors, cancellationToken);
        return vectors.Count;
    }
}
