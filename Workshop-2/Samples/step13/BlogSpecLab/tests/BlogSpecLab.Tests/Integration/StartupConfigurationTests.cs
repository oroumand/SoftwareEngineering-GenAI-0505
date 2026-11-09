using Microsoft.Data.Sqlite;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Endpoints.Api.Security;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Infra.DataAccess.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlogSpecLab.Tests.Integration;

public sealed class StartupConfigurationTests
{
    [Fact]
    public void ValidConfigurationStartsWithoutCreatingDatabase()
    {
        using var factory = new BlogApiFactory();
        _ = factory.Server;
        Assert.False(File.Exists(factory.DatabasePath));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("whitespace")]
    [InlineData("malformed")]
    [InlineData("unsupported-key")]
    [InlineData("missing-data-source")]
    [InlineData("empty-data-source")]
    public void InvalidConnectionStringFailsBeforeHostStartsWithoutSensitiveDetails(string scenario)
    {
        // دادهٔ حساس در InlineData و نام آزمون قرار نمی‌گیرد؛ marker تصادفی فقط در حافظه است.
        var sensitiveMarker = Guid.NewGuid().ToString("N");
        string? connectionString = scenario switch
        {
            "missing" => null,
            "empty" => "",
            "whitespace" => "   ",
            "malformed" => $"Data Source=\"{sensitiveMarker}",
            "unsupported-key" => $"Data Source={sensitiveMarker};Credential={sensitiveMarker}",
            "missing-data-source" => "Pooling=False",
            "empty-data-source" => "Data Source=   ",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        using var factory = new BlogApiFactory(connectionString);
        var error = Record.Exception(() => { _ = factory.Server; });
        Assert.NotNull(error);

        var messages = new List<string>();
        for (Exception? current = error; current is not null; current = current.InnerException)
            messages.Add(current.Message);

        var message = string.Join('\n', messages);
        Assert.False(string.IsNullOrWhiteSpace(message));
        // Assert روی boolean مانع چاپ خود متن خطا یا مقدار تنظیمات هنگام شکست می‌شود.
        Assert.True(message.Contains("Connection", StringComparison.OrdinalIgnoreCase)
            || message.Contains("اتصال", StringComparison.Ordinal));
        Assert.False(message.Contains(sensitiveMarker, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(connectionString))
            Assert.False(message.Contains(connectionString, StringComparison.Ordinal));
        Assert.False(File.Exists(factory.DatabasePath));
    }

    [Fact]
    public void NormalStartupDoesNotMigrateOrProvisionAnEmptyDatabase()
    {
        using var factory = new BlogApiFactory();
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = factory.DatabasePath,
            Pooling = false
        }.ToString());
        connection.Open();

        // فایل واقعی بدون schema و بدون کاربر؛ Startup نباید آن را آماده‌سازی کند.
        _ = factory.Server;
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
    }

    [Fact]
    public async Task MigratedDatabaseWithoutProvisionedUsersStartsNormally()
    {
        await using var factory = new BlogApiFactory();
        // Migration مستقل از Host و پیش از راه‌اندازی آن روی فایل خالی اعمال می‌شود.
        await using (var db = new BlogDbContext(new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = factory.DatabasePath, Pooling = false }.ToString()).Options))
        { await db.Database.MigrateAsync(TestContext.Current.CancellationToken); Assert.Empty(await db.UserAccess.ToListAsync(TestContext.Current.CancellationToken)); }
        _ = factory.Server;
        await using var scope = factory.Services.CreateAsyncScope();
        var actual = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        Assert.Single(await actual.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await actual.UserAccess.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProvisionAuthenticationAndCurrentPermissionUseTheMigratedDatabase()
    {
        await using var factory = new BlogApiFactory();
        var ct = TestContext.Current.CancellationToken;
        IReadOnlyList<ProvisionedAccess> issued = [];
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>(); await db.Database.MigrateAsync(ct);
            var roles = Enumerable.Repeat(LocalAccessRole.Author, 5).Concat(Enumerable.Repeat(LocalAccessRole.Editor, 5)).Append(LocalAccessRole.AuthorEditor).ToArray();
            var result = await scope.ServiceProvider.GetRequiredService<AccessProvisioner>().ProvisionAsync(roles, DateTime.UtcNow.AddHours(1),
                (values, _) => { issued = values; return Task.CompletedTask; }, ct);
            Assert.True(result.IsSuccess); Assert.Equal(11, issued.Count);
            var hashes = await db.Credentials.Select(x => x.TokenHash).ToArrayAsync(ct);
            Assert.Equal(11, hashes.Distinct().Count());
            Assert.True(issued.All(x => x.Token.Length >= 64 && !hashes.Contains(x.Token)));
        }
        var user = issued[0];
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Headers.Authorization = "Bearer " + user.Token;
            context.Request.Headers["X-Role"] = "Editor";
            var authentication = await context.AuthenticateAsync(OpaqueBearerHandler.SchemeName);
            Assert.True(authentication.Succeeded); Assert.Single(authentication.Principal!.Claims);
            var access = await scope.ServiceProvider.GetRequiredService<IUserAccessReader>().ReadAsync(user.UserId, ct);
            Assert.True(access!.CanWrite); Assert.False(access.CanReview);
        }
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<AccessProvisioner>().RevokePermissionsAsync(user.UserId, ct)).IsSuccess);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<ICredentialVerifier>().VerifyAsync(user.Token, ct)).IsSuccess);
            var access = await scope.ServiceProvider.GetRequiredService<IUserAccessReader>().ReadAsync(user.UserId, ct);
            Assert.False(access!.CanWrite || access.CanReview || access.CanPublish);
        }
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<AccessProvisioner>().RevokeCredentialAsync(user.CredentialId, ct)).IsSuccess);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var verifier = scope.ServiceProvider.GetRequiredService<ICredentialVerifier>();
            Assert.Equal(OperationError.Unauthenticated, (await verifier.VerifyAsync(user.Token, ct)).Failure!.Error);
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Headers.Authorization = "Bearer " + user.Token;
            Assert.False((await context.AuthenticateAsync(OpaqueBearerHandler.SchemeName)).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            Assert.Equal(11, await db.UserAccess.CountAsync(ct));
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var expired = await db.Credentials.SingleAsync(x => x.Id == issued[1].CredentialId, ct);
            expired.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(ct);
        }
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal(OperationError.Unauthenticated, (await scope.ServiceProvider.GetRequiredService<ICredentialVerifier>().VerifyAsync(issued[1].Token, ct)).Failure!.Error);
    }
}
