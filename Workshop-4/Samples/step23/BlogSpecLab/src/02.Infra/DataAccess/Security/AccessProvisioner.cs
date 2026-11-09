using System.Security.Cryptography;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess.Security;

public enum LocalAccessRole { Author, Editor, AuthorEditor }

public sealed class ProvisionedAccess
{
    public required string UserId { get; init; }
    public required Guid CredentialId { get; init; }
    public required string Token { get; init; }
    public override string ToString() => nameof(ProvisionedAccess);
}

public sealed class AccessProvisioner(BlogDbContext db, IWriteSession session, TimeProvider clock)
{
    // تحویل به secret storage پیش از Commit؛ خطای تحویل کل batch را rollback می‌کند.
    public async Task<OperationResult<Unit>> ProvisionAsync(IReadOnlyList<LocalAccessRole> roles,
        DateTime? expiresAt, Func<IReadOnlyList<ProvisionedAccess>, CancellationToken, Task> deliver,
        CancellationToken cancellationToken = default)
    {
        if (roles.Count == 0 || roles.Any(x => !Enum.IsDefined(x))
            || (expiresAt is { } expiry && (expiry.Kind != DateTimeKind.Utc || expiry <= clock.GetUtcNow().UtcDateTime)))
            return OperationResult<Unit>.Fail(OperationError.InvalidInput);
        var begin = await session.BeginAsync(cancellationToken);
        if (!begin.IsSuccess) return begin;
        try
        {
            var issued = new List<ProvisionedAccess>();
            foreach (var role in roles)
            {
                var userId = Guid.NewGuid().ToString("N");
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                var credentialId = Guid.NewGuid();
                db.UserAccess.Add(new UserAccessRecord
                {
                    UserId = userId, IsActive = true,
                    CanWrite = role is LocalAccessRole.Author or LocalAccessRole.AuthorEditor,
                    CanReview = role is LocalAccessRole.Editor or LocalAccessRole.AuthorEditor,
                    CanPublish = role is LocalAccessRole.Editor or LocalAccessRole.AuthorEditor
                });
                db.Credentials.Add(new Credential
                {
                    Id = credentialId, UserId = userId, TokenHash = CredentialVerifier.Hash(token), ExpiresAt = expiresAt
                });
                issued.Add(new() { UserId = userId, CredentialId = credentialId, Token = token });
            }
            await deliver(issued, cancellationToken);
            return await session.CommitAsync(cancellationToken);
        }
        finally { await session.RollbackAsync(CancellationToken.None); }
    }

    public Task<OperationResult<Unit>> RevokeCredentialAsync(Guid credentialId, CancellationToken cancellationToken = default) =>
        ChangeAsync(async () =>
        {
            var credential = await db.Credentials.SingleOrDefaultAsync(x => x.Id == credentialId, cancellationToken);
            if (credential is null) return false;
            credential.RevokedAt ??= clock.GetUtcNow().UtcDateTime;
            return true;
        }, cancellationToken);

    public Task<OperationResult<Unit>> RevokePermissionsAsync(string userId, CancellationToken cancellationToken = default) =>
        ChangeAsync(async () =>
        {
            var access = await db.UserAccess.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (access is null) return false;
            access.CanWrite = access.CanReview = access.CanPublish = false;
            return true;
        }, cancellationToken);

    private async Task<OperationResult<Unit>> ChangeAsync(Func<Task<bool>> change, CancellationToken cancellationToken)
    {
        var begin = await session.BeginAsync(cancellationToken);
        if (!begin.IsSuccess) return begin;
        try
        {
            if (!await change()) return OperationResult<Unit>.Fail(OperationError.InvalidInput);
            return await session.CommitAsync(cancellationToken);
        }
        catch (SqliteException) { return OperationResult<Unit>.Fail(OperationError.StorageUnavailable); }
        finally { await session.RollbackAsync(CancellationToken.None); }
    }
}
