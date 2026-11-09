namespace BlogSpecLab.Core.Application.Security;

public sealed record UserAccess
{
    public UserAccess(string userId, bool canWrite, bool canReview, bool canPublish, bool isActive)
    {
        UserId = userId;
        IsActive = isActive;
        CanWrite = isActive && canWrite;
        CanReview = isActive && canReview;
        CanPublish = isActive && canPublish;
    }

    public string UserId { get; }
    public bool IsActive { get; }
    public bool CanWrite { get; }
    public bool CanReview { get; }
    public bool CanPublish { get; }
}
