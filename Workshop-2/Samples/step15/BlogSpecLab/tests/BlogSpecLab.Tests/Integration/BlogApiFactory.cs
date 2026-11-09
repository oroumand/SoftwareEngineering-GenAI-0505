using BlogSpecLab.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace BlogSpecLab.Tests.Integration;

public sealed class BlogApiFactory : WebApplicationFactory<Program>
{
    private readonly string temporaryDirectory;
    private readonly string? connectionString;

    public BlogApiFactory() : this(null, useDefaultConnectionString: true) { }

    public BlogApiFactory(string? connectionString) : this(connectionString, useDefaultConnectionString: false) { }

    private BlogApiFactory(string? connectionString, bool useDefaultConnectionString)
    {
        temporaryDirectory = Path.Combine(Path.GetTempPath(), "BlogSpecLab.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        DatabasePath = Path.Combine(temporaryDirectory, "blog.db");
        this.connectionString = useDefaultConnectionString
            ? new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString()
            : connectionString;
    }

    public string DatabasePath { get; }
    public TestUsers Users { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:BlogDatabase", connectionString);
        // از handler واقعی میزبان استفاده می‌شود؛ هیچ bypass یا handler آزمونی ثبت نمی‌شود.
        // اتصال handler و provision/schema به‌ترتیب در T010 و T017 آماده می‌شوند.
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    public HttpClient CreateAuthenticatedClient(TestUser user)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        user.Authenticate(client);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            if (disposing)
                DeleteTemporaryDirectory();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            DeleteTemporaryDirectory();
        }
    }

    private void DeleteTemporaryDirectory()
    {
        // Pooling=false و Dispose میزبان پیش از حذف، اتصال‌های همین fixture را آزاد می‌کنند.
        if (Directory.Exists(temporaryDirectory))
            Directory.Delete(temporaryDirectory, recursive: true);
    }
}
