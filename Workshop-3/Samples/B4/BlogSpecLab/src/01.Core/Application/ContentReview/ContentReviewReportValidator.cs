namespace BlogSpecLab.Core.Application.ContentReview;

public sealed class ContentReviewReportValidator
{
    public bool IsValid(ContentReviewReport? report, ContentReviewSnapshot snapshot)
    {
        if (report is null || report.Status is not ("completed" or "needs_input") || Empty(report.Summary)
            || report.Issues.IsDefault || report.MissingInformation.IsDefault) return false;
        if ((report.Status == "completed") != report.MissingInformation.IsEmpty) return false;
        foreach (var issue in report.Issues)
            if (issue is null || issue.Category is not ("writing" or "ambiguity" or "internal_inconsistency" or "unsupported_claim")
                || !Evidence(issue.Field, issue.Evidence, snapshot) || Empty(issue.Explanation)
                || (issue.Suggestion is not null && Empty(issue.Suggestion))) return false;
        foreach (var missing in report.MissingInformation)
            if (missing is null || !Evidence(missing.Field, missing.Evidence, snapshot)
                || Empty(missing.Reason) || Empty(missing.RequestedInformation)) return false;
        return true;
    }
    private static bool Empty(string? value) => string.IsNullOrWhiteSpace(value);
    private static bool Evidence(string field, string evidence, ContentReviewSnapshot snapshot) =>
        !Empty(evidence) && field switch
        {
            "title" => snapshot.Title.Contains(evidence, StringComparison.Ordinal),
            "body" => snapshot.Body.Contains(evidence, StringComparison.Ordinal),
            _ => false
        };
}
