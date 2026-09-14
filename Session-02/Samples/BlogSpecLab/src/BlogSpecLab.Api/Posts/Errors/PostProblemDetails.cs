using Microsoft.AspNetCore.Mvc;

namespace BlogSpecLab.Api.Posts.Errors;

public static class PostProblemDetails
{
    public static ObjectResult Create(int status, string type, string title) => new(new ProblemDetails
    {
        Status = status,
        Type = type,
        Title = title
    })
    {
        StatusCode = status
    };
}
