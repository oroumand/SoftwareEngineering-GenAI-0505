using Microsoft.Data.Sqlite;
using Xunit;

namespace BlogSpecLab.Tests.Integration;

public sealed class StartupConfigurationTests
{
    private const string AwaitingSchema = "آزمون روی Schema واقعی بدون کاربران و مسیر Provision/Authentication پس از Migration اولیهٔ T017 اجرا می‌شود.";

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

    [Fact(Skip = AwaitingSchema)]
    public void MigratedDatabaseWithoutProvisionedUsersStartsNormally() =>
        throw new InvalidOperationException(AwaitingSchema);

    [Fact(Skip = AwaitingSchema)]
    public void ProvisionAuthenticationAndCurrentPermissionUseTheMigratedDatabase() =>
        throw new InvalidOperationException(AwaitingSchema);
}
