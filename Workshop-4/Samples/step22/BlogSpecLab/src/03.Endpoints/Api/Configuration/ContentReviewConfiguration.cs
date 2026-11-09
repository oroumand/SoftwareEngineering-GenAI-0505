using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Endpoints.Api.Http;
using BlogSpecLab.Infra.AI.ContentReview;
using BlogSpecLab.Infra.DataAccess.Posts;
namespace BlogSpecLab.Endpoints.Api.Configuration;
public static class ContentReviewConfiguration
{
    public static void AddContentReview(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection("AI:ContentReview");
        var settings = new ContentReviewOptions {
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            Model = section["Model"] ?? "gpt-6-luna",
            ReasoningEffort = section["ReasoningEffort"],
            MaxOutputTokenCount = section["MaxOutputTokenCount"] is null ? 8192
                : int.TryParse(section["MaxOutputTokenCount"], out var maximum) ? maximum : 0
        };
        // credential فقط از محیط میزبان؛ appsettings و ورودی Client منبع secret نیستند.
        var key = Environment.GetEnvironmentVariable("AI__ContentReview__ApiKey");
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<IContentReviewGateway>(_ => settings.Enabled && !string.IsNullOrWhiteSpace(key)
            && !string.IsNullOrWhiteSpace(settings.Model) && settings.MaxOutputTokenCount > 0
            && (settings.ReasoningEffort is null or "none" or "minimal" or "low" or "medium" or "high" or "xhigh")
            ? new OpenAiContentReviewGateway(key, settings) : new UnavailableContentReviewGateway());
        builder.Services.AddSingleton<IToolContentReviewGateway>(_ => settings.Enabled && !string.IsNullOrWhiteSpace(key)
            && !string.IsNullOrWhiteSpace(settings.Model) && settings.MaxOutputTokenCount > 0
            && (settings.ReasoningEffort is null or "none" or "minimal" or "low" or "medium" or "high" or "xhigh")
            ? new OpenAiToolContentReviewGateway(key, settings) : new UnavailableToolContentReviewGateway());
        // همان Use Case با Gateway متفاوت؛ کنترل Permission و State تکرار نمی‌شود.
        builder.Services.AddKeyedScoped<PostContentReviewService>("with-tools", (sp, _) =>
            new PostContentReviewService(sp.GetRequiredService<IPostContentReviewReader>(),
                sp.GetRequiredService<IUserAccessReader>(), sp.GetRequiredService<IToolContentReviewGateway>(),
                sp.GetRequiredService<ContentReviewReportValidator>(), sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<ContentReviewReportValidator>();
        builder.Services.AddScoped<IPostContentReviewReader, PostContentReviewReader>();
        builder.Services.AddScoped<PostContentReviewService>();
        builder.Services.AddSingleton<ContentReviewRequestCoordinator>();
        builder.Logging.AddFilter("OpenAI", LogLevel.None);
        builder.Logging.AddFilter("System.ClientModel", LogLevel.None);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.None);
    }
}
