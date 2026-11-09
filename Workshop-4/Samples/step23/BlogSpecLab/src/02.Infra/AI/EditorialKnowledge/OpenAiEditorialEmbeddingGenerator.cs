using BlogSpecLab.Core.Application.EditorialKnowledge;
using OpenAI.Embeddings;

namespace BlogSpecLab.Infra.AI.EditorialKnowledge;

/// <summary>تولید Embedding برای سندهای تأییدشده، مستقل از مدل تولید متن.</summary>
public sealed class OpenAiEditorialEmbeddingGenerator : IEditorialEmbeddingGenerator
{
    private readonly EmbeddingClient client;
    public string ModelId { get; }

    public OpenAiEditorialEmbeddingGenerator(string modelId, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("تنظیم مدل یا Credential مربوط به Embedding نامعتبر است.");
        ModelId = modelId;
        client = new EmbeddingClient(modelId, apiKey);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("متن Embedding خالی است.", nameof(text));
        var embedding = await client.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return embedding.Value.ToFloats().ToArray();
    }
}
