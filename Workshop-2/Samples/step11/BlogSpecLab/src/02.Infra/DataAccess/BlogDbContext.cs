using BlogSpecLab.Infra.DataAccess.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BlogSpecLab.Infra.DataAccess;

public sealed class BlogDbContext(DbContextOptions<BlogDbContext> options) : DbContext(options)
{
    public DbSet<UserAccessRecord> UserAccess => Set<UserAccessRecord>();
    public DbSet<Credential> Credentials => Set<Credential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
}
