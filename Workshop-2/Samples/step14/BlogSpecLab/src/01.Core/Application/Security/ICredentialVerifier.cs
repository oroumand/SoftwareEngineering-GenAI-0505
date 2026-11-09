using BlogSpecLab.Core.Application.Common;

namespace BlogSpecLab.Core.Application.Security;

public interface ICredentialVerifier
{
    Task<OperationResult<string>> VerifyAsync(string token, CancellationToken cancellationToken = default);
}
