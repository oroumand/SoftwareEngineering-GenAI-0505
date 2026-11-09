using System.Text.Json;
using BlogSpecLab.Core.Application.ContentReview;

namespace BlogSpecLab.Infra.AI.ContentReview;

// فقط snapshot از پیش مجازِ درخواست جاری؛ شناسهٔ پست یا نویسنده از مدل دریافت نمی‌شود.
public static class ReadCurrentPostTool
{
    public const string Name = "read_current_post";
    public const string Parameters = """
        {
          "type": "object",
          "properties": {
            "scope": { "type": "string", "enum": ["current_post"] }
          },
          "required": ["scope"],
          "additionalProperties": false
        }
        """;

    public static bool TryExecute(string? arguments, ContentReviewInput input, out string result)
    {
        result = string.Empty;
        if (string.IsNullOrWhiteSpace(arguments)) return false;
        try
        {
            using var document = JsonDocument.Parse(arguments, new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 1 || properties[0].Name != "scope" ||
                properties[0].Value.ValueKind != JsonValueKind.String ||
                properties[0].Value.GetString() != "current_post") return false;
            result = JsonSerializer.Serialize(new
            {
                title = input.Title,
                body = input.Body,
                titleUtf16Length = input.Title.Length,
                bodyUtf16Length = input.Body.Length
            });
            return true;
        }
        catch (JsonException) { return false; }
    }
}
