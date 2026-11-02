using BlogSpecLab.Core.Application.ContentReview;
namespace BlogSpecLab.Endpoints.Api.Contracts;
public sealed record ContentReviewResponse(string SchemaVersion, Guid PostId, ContentReviewSource Source,
    ContentReviewFreshness Freshness, ContentReviewReport Report)
{
    public static ContentReviewResponse From(ContentReviewEnvelope envelope) =>
        new(envelope.SchemaVersion, envelope.PostId, envelope.Source, envelope.Freshness, envelope.Report);
}
