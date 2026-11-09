using BlogSpecLab.Core.Application.ContentReview;
namespace BlogSpecLab.Infra.AI.ContentReview;
public sealed class UnavailableContentReviewGateway : IContentReviewGateway
{
    public Task<ContentReviewGatewayResult> ReviewAsync(ContentReviewInput input, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(new ContentReviewGatewayResult(ContentReviewOutcome.Unavailable)); }
}
