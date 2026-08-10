namespace BlogSpecLab.Domain.Posts.Errors;

public class PostDomainException(string message) : Exception(message);

public sealed class PostValidationException(string message) : PostDomainException(message);

public sealed class PostOwnershipException(string message) : PostDomainException(message);

public sealed class PostStateException(string message) : PostDomainException(message);
