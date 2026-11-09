using Microsoft.Data.Sqlite;

namespace BlogSpecLab.Endpoints.Api.Configuration;

public static class StartupConfiguration
{
    public static string Validate(IConfiguration configuration)
    {
        var value = configuration.GetConnectionString("BlogDatabase");
        var valid = false;
        if (!string.IsNullOrWhiteSpace(value))
        {
            try
            {
                var parsed = new SqliteConnectionStringBuilder(value);
                valid = !string.IsNullOrWhiteSpace(parsed.DataSource) && !parsed.DataSource.Contains('\0');
            }
            catch (ArgumentException) { }
        }
        if (!valid)
            throw new InvalidOperationException("تنظیمات اتصال Persistence (ConnectionStrings:BlogDatabase) غایب یا نامعتبر است.");
        return value!;
    }
}
