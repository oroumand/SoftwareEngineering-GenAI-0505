namespace BlogSpecLab.Infra.AI.ContentReview;
public sealed record ContentReviewOptions
{
    public bool Enabled { get; init; }
    public string Model { get; init; } = "gpt-6-luna";
    public string? ReasoningEffort { get; init; }
    public int MaxOutputTokenCount { get; init; } = 8192;
}
