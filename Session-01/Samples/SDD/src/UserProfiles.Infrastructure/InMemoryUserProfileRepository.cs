using UserProfiles.Application.Abstractions;
using UserProfiles.Application.Exceptions;
using UserProfiles.Domain;

namespace UserProfiles.Infrastructure;

public sealed class InMemoryUserProfileRepository : IUserProfileRepository
{
    private readonly Dictionary<Guid, UserProfile> _profiles = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task AddAsync(UserProfile profile, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_profiles.Values.Any(candidate =>
                    string.Equals(candidate.Email, profile.Email, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DuplicateEmailException(profile.Email);
            }

            _profiles.Add(profile.Id, profile);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _profiles.GetValueOrDefault(id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<UserProfile>> ListAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _profiles.Values
                .OrderBy(profile => profile.CreatedAtUtc)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> EmailExistsAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _profiles.Values.Any(candidate =>
                string.Equals(candidate.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }
}

