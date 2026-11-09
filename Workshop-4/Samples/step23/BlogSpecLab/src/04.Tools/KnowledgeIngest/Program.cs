using System.Text;
using BlogSpecLab.Core.Application.EditorialKnowledge;
using BlogSpecLab.Infra.AI.EditorialKnowledge;

Console.OutputEncoding = Encoding.UTF8;
return await RunAsync(args);

static async Task<int> RunAsync(string[] arguments)
{
    if (arguments.Length != 1 || arguments[0] is not ("dry-run" or "ingest"))
    {
        Console.Error.WriteLine("Usage: KnowledgeIngest dry-run | ingest");
        return 2;
    }

    const string model = "text-embedding-3-small";
    const string collection = "blogspec_editorial_text_embedding_3_small_v1";
    const string sourceRelative = "docs/editorial/official-policy-v1.md";

    try
    {
        var root = FindProjectRoot();
        var markdown = await File.ReadAllTextAsync(Path.Combine(root, sourceRelative));
        var source = new EditorialKnowledgeSource(
            "editorial-policy", "1.0.0", "سیاست تحریریهٔ آموزشی", markdown);
        var chunks = MarkdownSectionChunker.Split(source);

        Console.WriteLine($"Document: {source.DocumentId}, version: {source.Version}, chunks: {chunks.Count}");
        foreach (var chunk in chunks)
            Console.WriteLine($"  {chunk.Id:D} | {chunk.Section} | sha256:{chunk.ContentSha256[..12]}");

        if (arguments[0] == "dry-run")
        {
            Console.WriteLine("Dry run: هیچ سرویس بیرونی فراخوانی نشده است.");
            return 0;
        }

        // داده‌های این مثال ساختگی‌اند؛ اسناد واقعی نباید بدون مجوز به Provider بیرونی ارسال شوند.
        var key = Environment.GetEnvironmentVariable("AI__EditorialKnowledge__ApiKey")
                  ?? Environment.GetEnvironmentVariable("AI__ContentReview__ApiKey");
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("OpenAI API key is missing from secure environment variables.");
            return 2;
        }

        var rawUrl = Environment.GetEnvironmentVariable("QDRANT_URL") ?? "http://localhost:6333";
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttps && (url.Scheme != Uri.UriSchemeHttp || !url.IsLoopback)) ||
            !string.IsNullOrEmpty(url.UserInfo) || url.Query.Length != 0 || url.Fragment.Length != 0 ||
            url.AbsolutePath != "/")
        {
            Console.Error.WriteLine("QDRANT_URL must be a local HTTP endpoint or a valid HTTPS service root.");
            return 2;
        }

        using var http = new HttpClient
        {
            BaseAddress = new Uri(url.AbsoluteUri.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(20)
        };
        var qdrantKey = Environment.GetEnvironmentVariable("QDRANT_API_KEY");
        if (!string.IsNullOrWhiteSpace(qdrantKey))
            http.DefaultRequestHeaders.TryAddWithoutValidation("api-key", qdrantKey);

        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var service = new EditorialKnowledgeIngestionService(
            new OpenAiEditorialEmbeddingGenerator(model, key),
            new QdrantEditorialKnowledgeStore(http, collection));
        var indexed = await service.IngestAsync(source, deadline.Token);
        Console.WriteLine($"Indexed: {indexed} points into Qdrant collection {collection}");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or HttpRequestException or
                                      InvalidOperationException or ArgumentException or OperationCanceledException or
                                      System.Text.Json.JsonException or System.ClientModel.ClientResultException)
    {
        // متن خام استثنا ممکن است URL/شناسه/دادهٔ حساس داشته باشد.
        Console.Error.WriteLine("Ingestion failed: " + exception.GetType().Name);
        return 1;
    }
}

static string FindProjectRoot()
{
    foreach (var initial in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var folder = new DirectoryInfo(initial);
        while (folder is not null)
        {
            if (File.Exists(Path.Combine(folder.FullName, "BlogSpecLab.slnx")))
                return folder.FullName;
            folder = folder.Parent;
        }
    }
    throw new DirectoryNotFoundException("Project root containing BlogSpecLab.slnx was not found.");
}
