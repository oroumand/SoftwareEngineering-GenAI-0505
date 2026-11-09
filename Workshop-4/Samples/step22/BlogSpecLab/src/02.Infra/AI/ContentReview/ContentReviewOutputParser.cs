using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using BlogSpecLab.Core.Application.ContentReview;
namespace BlogSpecLab.Infra.AI.ContentReview;

public static class ContentReviewOutputParser
{
    public const int MaximumBytes = 256 * 1024;
    public static string SchemaJson { get; } = ReadSchema();
    private static string ReadSchema()
    {
        using var stream = typeof(ContentReviewOutputParser).Assembly.GetManifestResourceStream("content_review_v1.schema.json")!;
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    public static ContentReviewReport? Parse(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaximumBytes) return null;
        try
        {
            using var document = JsonDocument.Parse(text, new() { MaxDepth = 16 });
            using var schema = JsonDocument.Parse(SchemaJson);
            var root = document.RootElement;
            if (!Matches(root, schema.RootElement)) return null;
            return new(root.GetProperty("status").GetString()!, root.GetProperty("summary").GetString()!,
                root.GetProperty("issues").EnumerateArray().Select(i => new ContentReviewIssue(
                    S(i, "category"), S(i, "field"), S(i, "evidence"), S(i, "explanation"), i.GetProperty("suggestion").GetString())).ToImmutableArray(),
                root.GetProperty("missingInformation").EnumerateArray().Select(i => new ContentReviewMissingInformation(
                    S(i, "field"), S(i, "evidence"), S(i, "reason"), S(i, "requestedInformation"))).ToImmutableArray());
        }
        catch (JsonException) { return null; }
    }
    private static string S(JsonElement element, string name) => element.GetProperty(name).GetString()!;
    // همین subset کوچک Schema قرارداد؛ بدون repair یا coercion.
    private static bool Matches(JsonElement value, JsonElement schema)
    {
        var type = schema.GetProperty("type");
        var kind = value.ValueKind switch { JsonValueKind.Object => "object", JsonValueKind.Array => "array",
            JsonValueKind.String => "string", JsonValueKind.Null => "null", _ => "unsupported" };
        if (type.ValueKind == JsonValueKind.Array ? !type.EnumerateArray().Any(t => t.GetString() == kind) : type.GetString() != kind) return false;
        if (schema.TryGetProperty("enum", out var choices) && !choices.EnumerateArray().Any(c => c.GetString() == value.GetString())) return false;
        if (kind == "object")
        {
            var properties = schema.GetProperty("properties"); var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
                if (!seen.Add(property.Name) || !properties.TryGetProperty(property.Name, out var child) || !Matches(property.Value, child)) return false;
            return schema.GetProperty("required").EnumerateArray().All(p => seen.Contains(p.GetString()!));
        }
        return kind != "array" || value.EnumerateArray().All(v => Matches(v, schema.GetProperty("items")));
    }
    public static ContentReviewGatewayResult ParseProtocol(string wire)
    {
        try
        {
            using var doc = JsonDocument.Parse(wire);
            var root = doc.RootElement;
            if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array) return Invalid();
            // امتناع صریح بر fragment و status پروتکل مقدم است.
            foreach (var item in output.EnumerateArray())
                if (item.TryGetProperty("content", out var parts) && parts.ValueKind == JsonValueKind.Array
                    && parts.EnumerateArray().Any(p => p.TryGetProperty("type", out var t) && t.GetString() == "refusal"))
                    return new(ContentReviewOutcome.Refused);
            if (S(root, "status") == "failed") return new(ContentReviewOutcome.Unavailable);
            if (S(root, "status") != "completed" || (root.TryGetProperty("incomplete_details", out var incomplete) && incomplete.ValueKind != JsonValueKind.Null)) return Invalid();
            string? text = null; int messages = 0;
            foreach (var item in output.EnumerateArray())
            {
                var type = S(item, "type"); if (type == "reasoning") continue;
                if (type != "message" || ++messages != 1 || S(item, "status") != "completed" || S(item, "role") != "assistant") return Invalid();
                var parts = item.GetProperty("content");
                if (parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() != 1 || S(parts[0], "type") != "output_text") return Invalid();
                text = S(parts[0], "text");
            }
            var report = text is null ? null : Parse(text);
            return report is null ? Invalid() : new(ContentReviewOutcome.Candidate, report,
                root.TryGetProperty("model", out var model) ? model.GetString() : null);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException) { return Invalid(); }
    }
    private static ContentReviewGatewayResult Invalid() => new(ContentReviewOutcome.InvalidOutput);
}
