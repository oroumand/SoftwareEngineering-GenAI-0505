using BlogSpecLab.Core.Application.Common;

namespace BlogSpecLab.Core.Application.Persistence;

// یک scope مشترک با reader/store؛ Begin پیش از خواندن مجوز و داده انجام می‌شود.
// شکست یا خروج بدون Commit باید تمام تغییرات را rollback کند. فرمان تجاری retry نمی‌شود.
public interface IWriteSession : IAsyncDisposable
{
    Task<OperationResult<Unit>> BeginAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<Unit>> CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
