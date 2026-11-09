namespace BlogSpecLab.Core.Application.Security;

public interface IUserAccessReader
{
    Task<UserAccess?> ReadAsync(string userId, CancellationToken cancellationToken = default);
}
