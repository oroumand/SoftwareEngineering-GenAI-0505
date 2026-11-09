namespace BlogSpecLab.Core.Application.EditorialKnowledge;

public sealed record EditorialKnowledgeSource(string DocumentId, string Version, string Title, string Markdown);
public sealed record EditorialKnowledgeChunk(Guid Id, string DocumentId, string Version, string Title,
    string Section, string Content, string ContentSha256);
public sealed record EditorialKnowledgeVector(EditorialKnowledgeChunk Chunk, string EmbeddingModel, float[] Vector);

public interface IEditorialEmbeddingGenerator
{
    string ModelId { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken);
}

public interface IEditorialKnowledgeStore
{
    Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken);
    Task UpsertAsync(IReadOnlyList<EditorialKnowledgeVector> points, CancellationToken cancellationToken);
}
