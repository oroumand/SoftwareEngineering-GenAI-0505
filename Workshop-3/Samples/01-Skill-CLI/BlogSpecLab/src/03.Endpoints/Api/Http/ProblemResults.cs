using BlogSpecLab.Core.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace BlogSpecLab.Endpoints.Api.Http;

public static class ProblemResults
{
    public static IResult FromFailure(OperationFailure failure) => new FailureResult(failure);

    public static IResult FromOperation<T>(OperationResult<T> result, Func<T, IResult> success) =>
        result.IsSuccess ? success(result.Value!) : FromFailure(result.Failure!);

    public static void NoStore(HttpContext context) => context.Response.Headers.CacheControl = "no-store";

    private sealed class FailureResult(OperationFailure failure) : IResult
    {
        public async Task ExecuteAsync(HttpContext context)
        {
            var (status, code, message) = Describe(failure.Error);
            NoStore(context);
            if (status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
            var problem = new ProblemDetails
            {
                Type = "about:blank", Title = message, Status = status, Detail = message,
                Instance = context.Request.Path
            };
            problem.Extensions["code"] = code;
            if (status == 400 && failure.Errors is not null) problem.Extensions["errors"] = failure.Errors;
            // Application فقط پس از بررسی دسترسی این اطلاعات را فراهم می‌کند.
            if (status is 409 or 412)
            {
                if (failure.CurrentStatus is not null) problem.Extensions["currentStatus"] = failure.CurrentStatus;
                if (failure.CurrentVersion is not null) problem.Extensions["currentVersion"] = failure.CurrentVersion;
            }
            await Results.Problem(problem).ExecuteAsync(context);
        }
    }

    private static (int Status, string Code, string Message) Describe(OperationError error) => error switch
    {
        OperationError.InvalidInput => (400, "invalid_input", "ورودی درخواست نامعتبر است."),
        OperationError.Unauthenticated => (401, "unauthenticated", "هویت معتبر لازم است."),
        OperationError.Forbidden => (403, "forbidden", "مجوز انجام این عملیات را ندارید."),
        OperationError.SelfApprovalForbidden => (403, "self_approval_forbidden", "تأیید پست خود مجاز نیست."),
        OperationError.PostNotFound => (404, "post_not_found", "پست در دسترس نیست."),
        OperationError.InvalidState => (409, "invalid_state", "عملیات با وضعیت جاری سازگار نیست؛ دوباره جزئیات را بخوانید."),
        OperationError.PublishedPostImmutable => (409, "published_post_immutable", "پست منتشرشده قابل تغییر نیست؛ دوباره جزئیات را بخوانید."),
        OperationError.ApprovalRequired => (409, "approval_required", "تأیید معتبر لازم است؛ دوباره جزئیات را بخوانید."),
        OperationError.StaleVersion => (412, "stale_version", "نسخهٔ درخواست قدیمی است؛ دوباره جزئیات را بخوانید."),
        OperationError.VersionRequired => (428, "version_required", "ارسال If-Match لازم است."),
        OperationError.StorageUnavailable => (503, "storage_unavailable", "ذخیره‌سازی موقتاً در دسترس نیست."),
        OperationError.AiReviewRefused => (422, "ai_review_refused", "سرویس از انجام این بررسی خودداری کرد."),
        OperationError.AiReviewInvalidOutput => (502, "ai_review_invalid_output", "گزارش قابل استفاده تولید نشد."),
        OperationError.AiReviewUnavailable => (503, "ai_review_unavailable", "سرویس بررسی موقتاً در دسترس نیست."),
        OperationError.AiReviewTimeout => (504, "ai_review_timeout", "مهلت بررسی پایان یافت؛ درخواست تازه لازم است."),
        _ => (500, "unexpected_error", "خطای غیرمنتظره رخ داد.")
    };
}
