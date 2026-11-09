using System.Globalization;
using System.Text.Json;
using BlogSpecLab.Infra.DataAccess.Security;
using Microsoft.AspNetCore.DataProtection;

namespace BlogSpecLab.Endpoints.Api.Operations;

public static class AccessCommands
{
    public static async Task<int> RunAsync(string[] args, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<AccessProvisioner>();
        string? secretFile = null;
        var completed = false;
        try
        {
            if (args.Length == 3 && args[1] == "provision"
                && DateTimeOffset.TryParseExact(args[2], "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expiresAt))
            {
                var roles = Enumerable.Repeat(LocalAccessRole.Author, 5)
                    .Concat(Enumerable.Repeat(LocalAccessRole.Editor, 5)).Append(LocalAccessRole.AuthorEditor).ToArray();
                var result = await provisioner.ProvisionAsync(roles, expiresAt.UtcDateTime, async (issued, ct) =>
                {
                    var root = Path.GetFullPath(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlogSpecLab", "Secrets"));
                    EnsureOutsideRepository(root);
                    Directory.CreateDirectory(root);
                    EnsureNoLinks(root);
                    var keyPath = Path.Combine(root, "keys");
                    EnsureNoLinks(keyPath);
                    var protector = DataProtectionProvider.Create(new DirectoryInfo(keyPath), builder =>
                    {
                        builder.SetApplicationName("BlogSpecLab.LocalAccess");
                        if (!OperatingSystem.IsWindows())
                            throw new PlatformNotSupportedException("Secret storage محلی این ابزار از Windows DPAPI استفاده می‌کند.");
                        builder.ProtectKeysWithDpapi();
                    })
                        .CreateProtector("BlogSpecLab.LocalAccess.v1");
                    secretFile = Path.Combine(root, Guid.NewGuid().ToString("N") + ".protected");
                    var payload = protector.Protect(JsonSerializer.Serialize(issued));
                    await using var output = new FileStream(secretFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    await using var writer = new StreamWriter(output);
                    await writer.WriteAsync(payload.AsMemory(), ct);
                }, cancellationToken);
                completed = result.IsSuccess;
                Console.WriteLine(completed ? "Provision تکمیل شد؛ بستهٔ رمزگذاری‌شده در secret storage محلی تحویل شد."
                    : "Provision ناموفق بود؛ آماده بودن Schema و ذخیره‌سازی را بررسی کنید.");
                return completed ? 0 : 1;
            }

            if (args.Length == 3 && args[1] == "revoke-credential" && Guid.TryParse(args[2], out var id))
            {
                var result = await provisioner.RevokeCredentialAsync(id, cancellationToken);
                Console.WriteLine(result.IsSuccess ? "Credential لغو شد." : "لغو Credential ناموفق بود.");
                return result.IsSuccess ? 0 : 1;
            }
            if (args.Length == 3 && args[1] == "revoke-permissions" && !string.IsNullOrWhiteSpace(args[2]))
            {
                var result = await provisioner.RevokePermissionsAsync(args[2], cancellationToken);
                Console.WriteLine(result.IsSuccess ? "مجوزهای جاری لغو شدند." : "لغو مجوز ناموفق بود.");
                return result.IsSuccess ? 0 : 1;
            }
            Console.Error.WriteLine("قالب فرمان: access provision <UTC-expiry> | access revoke-credential <UUID> | access revoke-permissions <UserId>");
            return 2;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine("عملیات اپراتوری ناموفق بود؛ تنظیمات، Schema و secret storage محلی را بررسی کنید.");
            return 1;
        }
        finally
        {
            if (!completed && secretFile is not null && File.Exists(secretFile)) File.Delete(secretFile);
        }
    }

    private static void EnsureOutsideRepository(string path)
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (Directory.Exists(Path.Combine(directory.FullName, ".specify"))
                    && path.StartsWith(directory.FullName.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Secret storage باید خارج مخزن باشد.");
        EnsureNoLinks(path);
    }

    private static void EnsureNoLinks(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException("مسیر secret storage نباید پیوند داشته باشد.");
    }
}
