using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using BlogSpecLab.Core.Application.ContentReview;
using OpenAI.Responses;
namespace BlogSpecLab.Infra.AI.ContentReview;

#pragma warning disable OPENAI001
public sealed class OpenAiContentReviewGateway : IContentReviewGateway
{
    private readonly ResponsesClient client;
    private readonly ContentReviewOptions settings;
    public OpenAiContentReviewGateway(string apiKey, ContentReviewOptions settings, HttpClient? transport = null)
    {
        this.settings = settings;
        var options = new ResponsesClientOptions {
            RetryPolicy = new ClientRetryPolicy(0), NetworkTimeout = TimeSpan.FromSeconds(50), EnableDistributedTracing = false,
            ClientLoggingOptions = new() { EnableLogging = false, EnableMessageLogging = false, EnableMessageContentLogging = false }
        };
        if (transport is not null) options.Transport = new HttpClientPipelineTransport(transport);
        client = new ResponsesClient(new ApiKeyCredential(apiKey), options);
    }
    public async Task<ContentReviewGatewayResult> ReviewAsync(ContentReviewInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = new CreateResponseOptions(settings.Model, [
            ResponseItem.CreateDeveloperMessageItem(ContentReviewPrompt.Instruction),
            ResponseItem.CreateUserMessageItem(JsonSerializer.Serialize(new { title = input.Title, body = input.Body }))])
        {
            StoredOutputEnabled = false, StreamingEnabled = false, MaxOutputTokenCount = settings.MaxOutputTokenCount,
            TextOptions = new() { TextFormat = ResponseTextFormat.CreateJsonSchemaFormat("content_review_v1",
                BinaryData.FromString(ContentReviewOutputParser.SchemaJson), jsonSchemaIsStrict: true) }
        };
        if (!string.IsNullOrWhiteSpace(settings.ReasoningEffort)) options.ReasoningOptions = new() { ReasoningEffortLevel = settings.ReasoningEffort };
        try
        {
            var result = await client.CreateResponseAsync(options, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return ContentReviewOutputParser.ParseProtocol(result.GetRawResponse().Content.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is ClientResultException or HttpRequestException or OperationCanceledException)
        { return new(ContentReviewOutcome.Unavailable); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        { return new(ContentReviewOutcome.InvalidOutput); }
    }
}
#pragma warning restore OPENAI001
