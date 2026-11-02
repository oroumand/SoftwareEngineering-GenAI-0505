using System.Text.Json;
namespace BlogSpecLab.Tests.AI;

// فقط پس از ثبت رأی انسان فراخوانی شود؛ nullها هرگز رأی مثبت محسوب نمی‌شوند.
public static class ContentReviewEvaluationScoring
{
    public sealed record Metrics(double Recall, double CleanRate, bool AllMissingInformationCorrect, bool Passed);
    public static Metrics Score(JsonElement corpus, JsonElement run)
    {
        var samples = run.GetProperty("samples").EnumerateArray().ToDictionary(s => s.GetProperty("caseId").GetString()!);
        int allGold = 0, matchedGold = 0, cleanTotal = 0, cleanPass = 0; bool missingCorrect = true;
        foreach (var item in corpus.GetProperty("cases").EnumerateArray())
        {
            var gold = item.GetProperty("goldIssues").EnumerateArray().Select(g => g.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
            allGold += gold.Count; bool clean = item.GetProperty("clean").GetBoolean(); if (clean) cleanTotal++;
            bool needsInput = item.GetProperty("missingInformation").GetArrayLength() > 0;
            if (!samples.TryGetValue(item.GetProperty("id").GetString()!, out var sample) || sample.GetProperty("outcome").GetString() != "Candidate")
            { if (needsInput) missingCorrect = false; continue; }
            var vote = sample.GetProperty("humanVote");
            var ids = vote.GetProperty("matchedGoldIds");
            if (ids.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("رأی انسانی کامل نشده است.");
            matchedGold += ids.EnumerateArray().Select(v => v.GetString()!).Distinct(StringComparer.Ordinal).Count(gold.Contains);
            var unsupported = vote.GetProperty("unsupportedIssue"); var fabricated = vote.GetProperty("fabricatedInformation");
            if (unsupported.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || fabricated.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidOperationException("رأی انسانی کامل نشده است.");
            var report = sample.GetProperty("report");
            if (clean && report.GetProperty("status").GetString() == "completed" && !unsupported.GetBoolean() && !fabricated.GetBoolean()) cleanPass++;
            if (needsInput)
            {
                var correct = vote.GetProperty("missingInformationCorrect");
                if (correct.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new InvalidOperationException("رأی انسانی کامل نشده است.");
                missingCorrect &= correct.GetBoolean() && !fabricated.GetBoolean() && report.GetProperty("status").GetString() == "needs_input";
            }
        }
        var recall = allGold == 0 ? 0 : (double)matchedGold / allGold;
        var cleanRate = cleanTotal == 0 ? 0 : (double)cleanPass / cleanTotal;
        return new(recall, cleanRate, missingCorrect, recall >= .9 && cleanRate >= .9 && missingCorrect);
    }
}
