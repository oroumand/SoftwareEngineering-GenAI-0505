namespace BlogSpecLab.Infra.DataAccess.Security;

public sealed class Credential
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class UserAccessRecord
{
    public required string UserId { get; set; }
    public bool CanWrite { get; set; }
    public bool CanReview { get; set; }
    public bool CanPublish { get; set; }
    public bool IsActive { get; set; }
}
