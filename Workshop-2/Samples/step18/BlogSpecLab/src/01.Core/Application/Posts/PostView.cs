using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.Posts;

public sealed record ReviewView(string Decision, string ReviewerId, DateTime DecidedAt, int ContentRevision, string? Reason);
public sealed record PublicationView(string PublisherId, DateTime PublishedAt, Guid ApprovedReviewId);
public sealed record PostView(Guid Id, string OwnerId, string Title, string Body, string Status,
    int ContentRevision, Guid Version, ReviewView? LastReview, PublicationView? Publication, IReadOnlyList<string> AllowedActions)
{
    public static PostView From(Post post, UserAccess access)
    {
        var review = post.Reviews.SingleOrDefault(x => x.Id == post.CurrentReviewId);
        var publication = post.Publication;
        var actions = new List<string>();
        if (access.IsActive && !post.IsDeleted && post.Status != PostStatus.Published)
        {
            if (access.CanWrite && access.UserId == post.OwnerId)
            {
                actions.Add("edit"); actions.Add("delete");
                if (post.Status is PostStatus.Draft or PostStatus.ChangesRequested) actions.Add("submit");
            }
            if (access.CanReview && post.Status == PostStatus.PendingReview)
            {
                actions.Add("requestChanges");
                if (access.UserId != post.OwnerId) actions.Add("approve");
            }
            if (access.CanPublish && post.Status == PostStatus.Approved && post.HasValidApproval()) actions.Add("publish");
        }
        return new(post.Id, post.OwnerId, post.Title, post.Body, post.Status.ToString(), post.ContentRevision, post.Version,
            review is null ? null : new(review.Decision.ToString(), review.ReviewerId, review.DecidedAt, review.ContentRevision, review.Reason),
            publication is null ? null : new(publication.PublisherId, publication.PublishedAt, publication.ApprovedReviewId), actions.ToArray());
    }
}
