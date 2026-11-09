namespace BlogSpecLab.Core.Application.Common;

public enum OperationError
{
    InvalidInput, Unauthenticated, Forbidden, SelfApprovalForbidden, PostNotFound,
    InvalidState, PublishedPostImmutable, ApprovalRequired, StaleVersion,
    VersionRequired, StorageUnavailable, Unexpected,
    AiReviewRefused, AiReviewInvalidOutput, AiReviewUnavailable, AiReviewTimeout
}

public sealed record OperationFailure(OperationError Error,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    string? CurrentStatus = null, Guid? CurrentVersion = null);

public sealed class OperationResult<T>
{
    private OperationResult(T? value, OperationFailure? failure) => (Value, Failure) = (value, failure);
    public T? Value { get; }
    public OperationFailure? Failure { get; }
    public bool IsSuccess => Failure is null;
    public static OperationResult<T> Success(T value) => new(value, null);
    public static OperationResult<T> Fail(OperationError error) => new(default, new(error));
    public static OperationResult<T> Fail(OperationFailure failure) => new(default, failure);
}

public readonly record struct Unit;

// جزئیات خطای provider و تنظیمات حساس از این مرز عبور نمی‌کنند.
public sealed class StorageUnavailableException : Exception
{
    public StorageUnavailableException() : base("ذخیره‌سازی در دسترس نیست.") { }
}
