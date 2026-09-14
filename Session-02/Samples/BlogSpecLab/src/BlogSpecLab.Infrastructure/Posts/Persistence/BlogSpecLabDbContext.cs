using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infrastructure.Posts.Persistence;

public sealed class BlogSpecLabDbContext(DbContextOptions<BlogSpecLabDbContext> options) : DbContext(options)
{
    public DbSet<PostRecord> Posts => Set<PostRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var post = modelBuilder.Entity<PostRecord>();
        post.ToTable("posts", table =>
            table.HasCheckConstraint("CK_posts_status_published_at", "(status = 'Draft' AND published_at IS NULL) OR (status = 'Published' AND published_at IS NOT NULL)"));
        post.HasKey(item => item.Id);
        post.Property(item => item.Id).HasColumnName("id");
        post.Property(item => item.AuthorId).HasColumnName("author_id").IsRequired();
        post.Property(item => item.Title).HasColumnName("title").IsRequired();
        post.Property(item => item.Content).HasColumnName("content").IsRequired();
        post.Property(item => item.Status).HasColumnName("status").IsRequired();
        post.Property(item => item.PublishedAt).HasColumnName("published_at");
        post.Property(item => item.VersionToken).HasColumnName("version_token").IsRequired().IsConcurrencyToken();
        post.Property(item => item.Xmin).HasColumnName("xmin").IsRowVersion();
    }
}
