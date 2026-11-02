using System.Collections.Immutable;

namespace BlogSpecLab.Core.Application.ContentReview;

public sealed record ContentReviewIssue(string Category, string Field, string Evidence, string Explanation, string? Suggestion);
public sealed record ContentReviewMissingInformation(string Field, string Evidence, string Reason, string RequestedInformation);
public sealed record ContentReviewReport(string Status, string Summary, ImmutableArray<ContentReviewIssue> Issues,
    ImmutableArray<ContentReviewMissingInformation> MissingInformation);
