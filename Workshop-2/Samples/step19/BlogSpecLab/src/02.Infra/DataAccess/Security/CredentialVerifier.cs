using System.Security.Cryptography;
using System.Text;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess.Security;

public sealed class CredentialVerifier(BlogDbContext db, TimeProvider clock) : ICredentialVerifier
{
    public async Task<OperationResult<string>> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return OperationResult<string>.Fail(OperationError.Unauthenticated);
        var hash = Hash(token);
        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            var userId = await db.Credentials.AsNoTracking()
                .Where(x => x.TokenHash == hash && x.RevokedAt == null
                    && (x.ExpiresAt == null || x.ExpiresAt > now))
                .Select(x => x.UserId).SingleOrDefaultAsync(cancellationToken);
            return userId is null ? OperationResult<string>.Fail(OperationError.Unauthenticated)
                : OperationResult<string>.Success(userId);
        }
        catch (SqliteException) { return OperationResult<string>.Fail(OperationError.StorageUnavailable); }
    }

    internal static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
