using System.Reflection;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Infra.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Tests.Fixtures;

// وضعیت‌های US2 فقط در fixture ساخته می‌شوند؛ مسیر تجاری بررسی/انتشار ایجاد نمی‌شود.
public static class PostStates
{
    public static Post In(PostStatus status, string owner = "author")
    {
        var post = Post.Create(owner, "عنوان", "متن");
        Set(post, nameof(Post.Status), status);
        if (status is PostStatus.ChangesRequested or PostStatus.Approved or PostStatus.Published)
        {
            var decision = new ReviewDecision(Guid.NewGuid(), post.Id, post.ContentRevision,
                post.Title, post.Body, status == PostStatus.ChangesRequested ? ReviewDecisionKind.RequestChanges : ReviewDecisionKind.Approve,
                "reviewer", DateTime.UtcNow, status == PostStatus.ChangesRequested ? "دلیل تاریخی" : null);
            ((List<ReviewDecision>)typeof(Post).GetField("reviews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(post)!).Add(decision);
            Set(post, nameof(Post.CurrentReviewId), decision.Id);
            if (status == PostStatus.Published)
                Set(post, nameof(Post.Publication), new PublicationInfo("publisher", DateTime.UtcNow, decision.Id));
        }
        return post;
    }

    public static async Task SaveAsync(BlogDbContext db, params Post[] posts)
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var post in posts)
        {
            var history = post.Reviews.ToArray(); var reviewId = post.CurrentReviewId; var publication = post.Publication;
            foreach (var userId in history.Select(r => r.ReviewerId).Concat(publication is null ? [] : new[] { publication.PublisherId }).Distinct())
                if (!await db.UserAccess.AnyAsync(u => u.UserId == userId, ct))
                    db.UserAccess.Add(new() { UserId = userId, IsActive = true });
            var reviews = (List<ReviewDecision>)typeof(Post).GetField("reviews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(post)!;
            reviews.Clear(); Set(post, nameof(Post.CurrentReviewId), null); Set(post, nameof(Post.Publication), null);
            db.Add(post); await db.SaveChangesAsync(ct);
            reviews.AddRange(history); db.ReviewDecisions.AddRange(history); await db.SaveChangesAsync(ct);
            Set(post, nameof(Post.CurrentReviewId), reviewId); Set(post, nameof(Post.Publication), publication);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
    private static void Set(Post post, string name, object? value) => typeof(Post).GetProperty(name)!.SetValue(post, value);
}
