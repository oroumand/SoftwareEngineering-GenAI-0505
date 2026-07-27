using Microsoft.AspNetCore.Mvc;
using UserProfiles.Application.Exceptions;
using UserProfiles.Domain;

namespace UserProfiles.Api;

public sealed class ApiExceptionMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainValidationException exception)
        {
            await WriteValidationProblemAsync(context, exception);
        }
        catch (DuplicateEmailException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Duplicate email",
                exception.Message);
        }
        catch (UserProfileNotFoundException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status404NotFound,
                "User profile not found",
                exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled error while processing {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteValidationProblemAsync(
        HttpContext context,
        DomainValidationException exception)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(
            new ValidationProblemDetails(
                exception.Errors.ToDictionary(
                    error => error.Key,
                    error => error.Value,
                    StringComparer.OrdinalIgnoreCase))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid user profile"
            });
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string detail)
    {
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            });
    }
}
