namespace BlogSpecLab.Application.Posts.Ports;

public abstract class UseCaseException(string message) : Exception(message);

public sealed class ValidationUseCaseException(string message) : UseCaseException(message);

public sealed class NotFoundUseCaseException(string message) : UseCaseException(message);

public sealed class PreconditionRequiredUseCaseException(string message) : UseCaseException(message);

public sealed class PreconditionFailedUseCaseException(string message) : UseCaseException(message);

public sealed class StateConflictUseCaseException(string message) : UseCaseException(message);
