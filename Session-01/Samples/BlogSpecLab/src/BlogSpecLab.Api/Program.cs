using BlogSpecLab.Api.Posts.Identity;
using BlogSpecLab.Application.Posts.CreateDraft;
using BlogSpecLab.Infrastructure.Posts.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentAuthorAccessor, HttpCurrentAuthorAccessor>();
builder.Services.AddScoped<CreateDraftHandler>();

var connectionString = builder.Configuration.GetConnectionString("BlogSpecLab")
    ?? throw new InvalidOperationException("اتصال پایگاه داده پیکربندی نشده است.");
builder.Services.AddPostPersistence(connectionString);

var app = builder.Build();
app.UseExceptionHandler();
app.MapControllers();
app.Run();

public partial class Program;
