using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Infra.AI.ContentReview;
using Xunit;
namespace BlogSpecLab.Tests.AI;
public class ContentReviewQualityTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("BLOG_AI_QUALITY_OPT_IN") == "true";
    [Fact(SkipUnless = nameof(Enabled))] [Trait("Category", "AIQuality")]
    public async Task Score_only_explicitly_supplied_human_votes()
    {
        var scoredInput = Environment.GetEnvironmentVariable("BLOG_AI_EVAL_SCORED_INPUT");
        if (string.IsNullOrWhiteSpace(scoredInput)) Assert.Skip("رأی انسانی برای scoring ارائه نشده؛ این skip شاهد پذیرش نیست.");
        using var run = JsonDocument.Parse(await File.ReadAllTextAsync(scoredInput!, TestContext.Current.CancellationToken));
        var corpusText = await File.ReadAllTextAsync(Path.Combine(ContentReviewTransportTests.FindRoot(), "tests/BlogSpecLab.Tests/Fixtures/ContentReviewAcceptanceCorpus.json"), TestContext.Current.CancellationToken);
        Assert.Equal(Hash(corpusText), run.RootElement.GetProperty("corpusSha256").GetString());
        using var corpus = JsonDocument.Parse(corpusText);
        var metrics = ContentReviewEvaluationScoring.Score(corpus.RootElement, run.RootElement);
        var path = scoredInput + ".metrics.json";
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, metrics, new JsonSerializerOptions { WriteIndented = true }, TestContext.Current.CancellationToken);
        Assert.True(metrics.Passed, "این run از Quality Gate عبور نکرد؛ threshold تغییر نکند.");
    }
    [Fact(SkipUnless = nameof(Enabled))] [Trait("Category", "AIQuality")]
    public async Task Generate_one_fixed_run_for_human_scoring()
    {
        var run = Environment.GetEnvironmentVariable("BLOG_AI_EVAL_RUN");
        Assert.Contains(run, new[] { "acceptance-1", "acceptance-2", "acceptance-3" });
        var root = ContentReviewTransportTests.FindRoot(); var corpusPath = Path.Combine(root, "tests/BlogSpecLab.Tests/Fixtures/ContentReviewAcceptanceCorpus.json");
        var corpusText = await File.ReadAllTextAsync(corpusPath, TestContext.Current.CancellationToken);
        var corpusHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(corpusText)));
        Assert.Equal(corpusHash, Environment.GetEnvironmentVariable("BLOG_AI_APPROVED_CORPUS_SHA256"));
        var destination = Environment.GetEnvironmentVariable("BLOG_AI_EVAL_OUTPUT_DIRECTORY"); Assert.False(string.IsNullOrWhiteSpace(destination));
        var gateway = ContentReviewProviderTests.Gateway(); var settings = ContentReviewProviderTests.Options();
        using var corpus = JsonDocument.Parse(corpusText); var rows = new JsonArray();
        foreach (var item in corpus.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input = new ContentReviewInput(item.GetProperty("title").GetString()!, item.GetProperty("body").GetString()!);
            using var timer = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timer.CancelAfter(TimeSpan.FromSeconds(50));
            var elapsed = Stopwatch.StartNew(); ContentReviewGatewayResult result;
            try { result = await gateway.ReviewAsync(input, timer.Token); }
            catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested) { result = new(ContentReviewOutcome.Timeout); }
            var snapshot = new ContentReviewSnapshot(Guid.Empty, "synthetic", input.Title, input.Body, 1, Core.Domain.Posts.PostStatus.Draft, DateTimeOffset.UtcNow);
            if (result.Outcome == ContentReviewOutcome.Candidate && !new ContentReviewReportValidator().IsValid(result.Report, snapshot)) result = new(ContentReviewOutcome.InvalidOutput);
            rows.Add(new JsonObject { ["caseId"] = item.GetProperty("id").GetString(), ["outcome"] = result.Outcome.ToString(),
                ["latencyMs"] = elapsed.Elapsed.TotalMilliseconds, ["actualModel"] = result.ProviderModel,
                ["report"] = result.Report is null ? null : JsonSerializer.SerializeToNode(result.Report, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                ["humanVote"] = new JsonObject { ["matchedGoldIds"] = null, ["unsupportedIssue"] = null, ["missingInformationCorrect"] = null, ["fabricatedInformation"] = null, ["notes"] = null } });
        }
        var output = new JsonObject { ["runId"] = run, ["createdAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["requestedModel"] = settings.Model, ["sdk"] = "OpenAI 2.14.0", ["promptVersion"] = ContentReviewPrompt.Version,
            ["schemaVersion"] = "content_review_v1", ["corpusVersion"] = corpus.RootElement.GetProperty("version").GetString(),
            ["corpusSha256"] = corpusHash, ["schemaSha256"] = Hash(ContentReviewOutputParser.SchemaJson), ["promptSha256"] = Hash(ContentReviewPrompt.Instruction),
            ["reasoningEffort"] = settings.ReasoningEffort ?? "provider_default_unmeasured", ["maxOutputTokens"] = settings.MaxOutputTokenCount,
            ["providerBudgetSeconds"] = 50, ["acceptanceStatus"] = "awaiting_human_votes", ["samples"] = rows };
        Directory.CreateDirectory(destination!);
        var path = Path.Combine(destination!, run + ".json");
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, output, new JsonSerializerOptions { WriteIndented = true }, TestContext.Current.CancellationToken);
        Assert.Equal(30, rows.Count);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
