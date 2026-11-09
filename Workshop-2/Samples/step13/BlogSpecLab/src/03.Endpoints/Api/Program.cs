using System.Text.Json.Serialization;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Persistence;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Infra.DataAccess.Posts;
using BlogSpecLab.Endpoints.Api.Endpoints;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Endpoints.Api.Configuration;
using BlogSpecLab.Endpoints.Api.Http;
using BlogSpecLab.Endpoints.Api.Operations;
using BlogSpecLab.Endpoints.Api.Security;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Infra.DataAccess.Persistence;
using BlogSpecLab.Infra.DataAccess.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var operatorMode = args.FirstOrDefault() == "access";
var builder = WebApplication.CreateBuilder(operatorMode ? [] : args);
var connectionString = StartupConfiguration.Validate(builder.Configuration);
// خطاهای provider ممکن است دادهٔ خصوصی یا جزئیات اتصال داشته باشند.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Services.AddDbContext<BlogDbContext>(options => options.UseSqlite(connectionString, sqlite => sqlite.CommandTimeout(1)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICredentialVerifier, CredentialVerifier>();
builder.Services.AddScoped<IUserAccessReader, UserAccessReader>();
builder.Services.AddScoped<IWriteSession, SqliteWriteSession>();
builder.Services.AddScoped<AccessProvisioner>();
builder.Services.AddScoped<IPostStore, PostStore>();
builder.Services.AddScoped<AuthorPostService>();
builder.Services.AddScoped<EditorPostService>();
builder.Services.AddAuthentication(OpaqueBearerHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, OpaqueBearerHandler>(OpaqueBearerHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
var app = builder.Build();

if (operatorMode)
{
    Environment.ExitCode = await AccessCommands.RunAsync(args, app.Services);
    await app.DisposeAsync();
    return;
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/author") || context.Request.Path.StartsWithSegments("/api/editor"))
        ProblemResults.NoStore(context);
    context.Response.OnStarting(() =>
    {
        if (context.Response.StatusCode >= 400) ProblemResults.NoStore(context);
        return Task.CompletedTask;
    });
    try
    {
        await next(context);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
    catch (Exception error)
    {
        if (context.Response.HasStarted)
        {
            context.Abort();
            return;
        }
        context.Response.Clear();
        var failure = new OperationFailure(error is StorageUnavailableException
            ? OperationError.StorageUnavailable : OperationError.Unexpected);
        await ProblemResults.FromFailure(failure).ExecuteAsync(context);
    }
});
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthorPosts();
app.MapEditorPosts();
await app.RunAsync();

public partial class Program;

