using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BlogSpecLab.Infra.DataAccess.Persistence;

public sealed class SqliteWriteSession(BlogDbContext db) : IWriteSession
{
    private SqliteTransaction? transaction;
    private IDbContextTransaction? enlistedTransaction;
    private bool started;
    private bool finished;

    public async Task<OperationResult<Unit>> BeginAsync(CancellationToken cancellationToken = default)
    {
        if (started || finished) throw new InvalidOperationException("نشست نوشتن قابل تکرار نیست.");
        started = true;
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        // هر تلاش گرفتن lock حداکثر یک ثانیه؛ retry فقط پیش از خواندن و تصمیم است.
        connection.DefaultTimeout = 1;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                    await db.Database.OpenConnectionAsync(cancellationToken);
                transaction = connection.BeginTransaction(deferred: false);
                enlistedTransaction = await db.Database.UseTransactionAsync(transaction, cancellationToken);
                return OperationResult<Unit>.Success(default);
            }
            catch (SqliteException error) when (transaction is null && error.SqliteErrorCode is 5 or 6 && attempt < 2)
            {
                // هیچ فرمان یا تصمیم Application در این حلقه اجرا نمی‌شود.
            }
            catch (SqliteException)
            {
                await RollbackAsync(CancellationToken.None);
                return OperationResult<Unit>.Fail(OperationError.StorageUnavailable);
            }
            catch
            {
                await RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        throw new InvalidOperationException();
    }

    public async Task<OperationResult<Unit>> CommitAsync(CancellationToken cancellationToken = default)
    {
        if (transaction is null || finished) throw new InvalidOperationException("تراکنش نوشتن فعال نیست.");
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            finished = true;
            await ReleaseAsync();
            return OperationResult<Unit>.Success(default);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackAsync(CancellationToken.None);
            return OperationResult<Unit>.Fail(OperationError.StaleVersion);
        }
        catch (Exception error) when (error is SqliteException || error is DbUpdateException { InnerException: SqliteException })
        {
            await RollbackAsync(CancellationToken.None);
            return OperationResult<Unit>.Fail(OperationError.StorageUnavailable);
        }
        catch
        {
            await RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (finished) return;
        try
        {
            if (transaction is not null && !finished)
                await transaction.RollbackAsync(cancellationToken);
        }
        catch (SqliteException) { /* آزادسازی اتصال همچنان باید انجام شود. */ }
        finally
        {
            finished = true;
            db.ChangeTracker.Clear();
            await ReleaseAsync();
        }
    }

    private async Task ReleaseAsync()
    {
        if (enlistedTransaction is not null) await enlistedTransaction.DisposeAsync();
        if (transaction is not null) await transaction.DisposeAsync();
        enlistedTransaction = null;
        transaction = null;
        await db.Database.CloseConnectionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!finished) await RollbackAsync(CancellationToken.None);
    }
}
