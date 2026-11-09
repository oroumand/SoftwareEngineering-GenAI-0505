using BlogSpecLab.Infra.DataAccess.Security;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Infra.DataAccess.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BlogSpecLab.Infra.DataAccess;

public sealed class BlogDbContext(DbContextOptions<BlogDbContext> options) : DbContext(options)
{
    public DbSet<UserAccessRecord> UserAccess => Set<UserAccessRecord>();
    public DbSet<Credential> Credentials => Set<Credential>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<ReviewDecision> ReviewDecisions => Set<ReviewDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PostConfiguration());
        modelBuilder.ApplyConfiguration(new ReviewDecisionConfiguration());
        var access = modelBuilder.Entity<UserAccessRecord>();
        access.ToTable("UserAccess");
        access.HasKey(x => x.UserId);
        var credential = modelBuilder.Entity<Credential>();
        credential.HasKey(x => x.Id);
        credential.Property(x => x.TokenHash).IsRequired();
        credential.HasIndex(x => x.TokenHash).IsUnique();
        var utc = new ValueConverter<DateTime, DateTime>(
            value => value.ToUniversalTime(), value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        credential.Property(x => x.ExpiresAt).HasConversion(utc);
        credential.Property(x => x.RevokedAt).HasConversion(utc);
        credential.HasOne<UserAccessRecord>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { RejectHistoryChanges(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    { RejectHistoryChanges(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    private void RejectHistoryChanges()
    {
        if (ChangeTracker.Entries<ReviewDecision>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("تصمیم تاریخی قابل تغییر نیست.");
    }
}
