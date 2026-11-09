using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Infra.DataAccess.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BlogSpecLab.Infra.DataAccess.Posts;

public sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> post)
    {
        post.HasKey(x => x.Id);
        post.Property(x => x.OwnerId).IsRequired();
        post.Property(x => x.Title).IsRequired(); post.Property(x => x.Body).IsRequired();
        post.Property(x => x.Status).HasConversion<string>();
        post.Property(x => x.Version).IsConcurrencyToken();
        post.HasQueryFilter(x => !x.IsDeleted);
        post.HasIndex(x => new { x.OwnerId, x.IsDeleted }); post.HasIndex(x => new { x.Status, x.IsDeleted });
        post.HasOne<UserAccessRecord>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        post.HasMany(x => x.Reviews).WithOne().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
        post.Navigation(x => x.Reviews).HasField("reviews").UsePropertyAccessMode(PropertyAccessMode.Field);
        post.HasOne<ReviewDecision>().WithMany().HasForeignKey(x => x.CurrentReviewId).OnDelete(DeleteBehavior.Restrict);
        var utc = new ValueConverter<DateTime, DateTime>(value => value.ToUniversalTime(), value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        post.OwnsOne(x => x.Publication, publication =>
        {
            publication.Property(x => x.PublisherId).IsRequired();
            publication.Property(x => x.PublishedAt).HasConversion(utc); publication.Property(x => x.ApprovedReviewId);
            publication.HasOne<UserAccessRecord>().WithMany().HasForeignKey(x => x.PublisherId).OnDelete(DeleteBehavior.Restrict);
            publication.HasOne<ReviewDecision>().WithMany().HasForeignKey(x => x.ApprovedReviewId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class ReviewDecisionConfiguration : IEntityTypeConfiguration<ReviewDecision>
{
    public void Configure(EntityTypeBuilder<ReviewDecision> review)
    {
        review.HasKey(x => x.Id); review.Property(x => x.PostId); review.Property(x => x.ContentRevision);
        review.Property(x => x.ReviewedTitle).IsRequired(); review.Property(x => x.ReviewedBody).IsRequired();
        review.Property(x => x.Decision).HasConversion<string>();
        review.Property(x => x.ReviewerId).IsRequired(); review.Property(x => x.Reason);
        review.Property(x => x.DecidedAt).HasConversion(new ValueConverter<DateTime, DateTime>(value => value.ToUniversalTime(), value => DateTime.SpecifyKind(value, DateTimeKind.Utc)));
        review.HasOne<UserAccessRecord>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
    }
}
