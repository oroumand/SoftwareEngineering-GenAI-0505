using BlogSpecLab.Core.Application.Posts;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BlogSpecLab.Endpoints.Api.Http;

public static class OpenApiMetadata
{
    public static IServiceCollection AddBlogOpenApi(this IServiceCollection services) => services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<PostOpenApiTransformer>();
        options.AddOperationTransformer((operation, context, ct) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata.OfType<PostHttpMetadata>().SingleOrDefault();
            if (metadata?.IfMatch == true)
            {
                operation.Parameters ??= [];
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "If-Match", In = ParameterLocation.Header, Required = true,
                    Description = "یک ETag قوی UUID داخل کوتیشن؛ wildcard و چند مقدار نامعتبر. نبود: 428؛ قالب نامعتبر: 400.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = "^\"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\"$" }
                });
            }
            if (metadata?.ETag == true)
                foreach (var response in operation.Responses!.Where(x => x.Key is "200" or "201"))
                {
                    var value = (OpenApiResponse)response.Value;
                    value.Headers ??= new Dictionary<string, IOpenApiHeader>();
                    value.Headers["ETag"] = new OpenApiHeader
                    {
                        Description = "Version به شکل UUID داخل کوتیشن",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                    };
                }
            return Task.CompletedTask;
        });
        options.AddSchemaTransformer((schema, context, ct) =>
        {
            if (context.JsonTypeInfo.Type == typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))
            {
                schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
                schema.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "کد پایدار خطا؛ مقادیر در توضیح پاسخ HTTP" };
                schema.Required ??= new HashSet<string>();
                foreach (var field in new[] { "type", "title", "status", "detail", "instance", "code" }) schema.Required.Add(field);
                schema.Properties["currentStatus"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "فقط تعارض 409/412 و پس از کنترل دسترسی" };
                schema.Properties["currentVersion"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid", Description = "فقط تعارض 409/412 و پس از کنترل دسترسی" };
                schema.Properties["errors"] = new OpenApiSchema { Type = JsonSchemaType.Object, Description = "خطاهای فیلدی اختیاری title، body یا reason در 400" };
            }
            if (context.JsonTypeInfo.Type == typeof(PostView))
            {
                schema.Properties!["status"].Description = "Draft، PendingReview، ChangesRequested، Approved، Published";
                schema.Properties["allowedActions"].Description = "بدون تکرار و به ترتیب edit، delete، submit، requestChanges، approve، publish؛ وابسته به مجوز جاری و وضعیت. Published آرایهٔ خالی؛ تضمین مجوز درخواست بعدی نیست.";
            }
            if (context.JsonTypeInfo.Type == typeof(Contracts.PostContentRequest) || context.JsonTypeInfo.Type == typeof(Contracts.RequestChangesRequest))
            {
                schema.AdditionalPropertiesAllowed = false;
                foreach (var property in schema.Properties!.Values)
                {
                    var value = (OpenApiSchema)property;
                    value.Type = JsonSchemaType.String;
                    value.MinLength = 1;
                    value.Description = "غیر null، غیرخالی و غیر whitespace؛ بدون سقف طول کسب‌وکاری";
                }
            }
            return Task.CompletedTask;
        });
    });

    public static RouteHandlerBuilder DocumentPost(this RouteHandlerBuilder route, bool list = false,
        bool mutation = false, int success = 200, bool etag = true)
    {
        if (success == 204) route.Produces(204);
        else if (list) route.Produces<PostView[]>(success);
        else route.Produces<PostView>(success);
        route.ProducesProblem(401).ProducesProblem(403).ProducesProblem(503).ProducesProblem(500);
        if (!list && success != 201) route.ProducesProblem(404);
        if (mutation) route.ProducesProblem(400);
        if (mutation && success != 201)
            route.ProducesProblem(409).ProducesProblem(412).ProducesProblem(428);
        return route.WithMetadata(new PostHttpMetadata(mutation && success != 201, etag && !list && success != 204));
    }

    public static RouteHandlerBuilder DocumentPublic(this RouteHandlerBuilder route, bool list = false)
    {
        if (list) route.Produces<PublicPostView[]>();
        else route.Produces<PublicPostView>().ProducesProblem(404);
        return route.ProducesProblem(503).ProducesProblem(500);
    }
}

public sealed record PostHttpMetadata(bool IfMatch, bool ETag);

public sealed class PostOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "BlogSpecLab";
        document.Info.Description = "مدیریت و انتشار کنترل‌شدهٔ پست؛ قرارداد مرجع: contracts/http-api.md";
        document.Components ??= new();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "opaque credential" }
        };
        foreach (var path in document.Paths)
        foreach (var operation in path.Value.Operations!.Values)
        {
            var management = path.Key.StartsWith("/api/author") || path.Key.StartsWith("/api/editor");
            if (management)
                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            operation.Description = management
                ? "مجوز و مالکیت از دادهٔ جاری سرور کنترل می‌شوند. allowedActions فقط راهنماست. فهرست به ترتیب Id؛ بدون صفحه‌بندی."
                : "خواندن ناشناس فقط Published حذف‌نشده؛ PublicPostView فاقد دادهٔ مدیریتی است. فهرست به ترتیب Id.";
            if (path.Key.EndsWith("/publish")) operation.Description += " انتشار تکراری با مجوز فعلی و If-Match معتبر، حتی نسخهٔ قدیمی، همان انتشار نخست را بدون Commit تازه برمی‌گرداند.";
            foreach (var response in operation.Responses!)
            {
                var value = (OpenApiResponse)response.Value;
                value.Headers ??= new Dictionary<string, IOpenApiHeader>();
                if (management || int.Parse(response.Key) >= 400)
                    value.Headers["Cache-Control"] = Header("no-store");
                if (response.Key == "401") value.Headers["WWW-Authenticate"] = Header("Bearer");
                if (response.Key == "201") value.Headers["Location"] = Header("مسیر جزئیات پست ایجادشده");
                response.Value.Description = response.Key switch
                {
                    "400" => "invalid_input",
                    "401" => "unauthenticated",
                    "403" => path.Key.EndsWith("/approve") ? "forbidden / self_approval_forbidden" : "forbidden",
                    "404" => "post_not_found",
                    "409" => path.Key.EndsWith("/publish") ? "invalid_state / approval_required؛ دوباره جزئیات را بخوانید"
                        : "invalid_state / published_post_immutable؛ دوباره جزئیات را بخوانید",
                    "412" => "stale_version؛ دوباره جزئیات را بخوانید",
                    "428" => "version_required",
                    "503" => "storage_unavailable",
                    "500" => "unexpected_error؛ بدون جزئیات داخلی",
                    _ => "موفق"
                };
            }
        }
        return Task.CompletedTask;
    }

    private static OpenApiHeader Header(string description) => new() { Description = description, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
}
