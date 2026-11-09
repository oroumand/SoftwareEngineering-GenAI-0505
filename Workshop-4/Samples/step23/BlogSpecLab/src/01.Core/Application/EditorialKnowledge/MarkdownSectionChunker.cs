using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BlogSpecLab.Core.Application.EditorialKnowledge;

/// <summary>
/// بخش‌بندی بر پایهٔ عنوان سطح دوم؛ قواعد و استثناهای یک بخش از هم جدا نمی‌شوند.
/// سند آموزشی باید بخش‌های کوچک (کمتر از ۲۰۰۰ نویسه) داشته باشد.
/// </summary>
public static class MarkdownSectionChunker
{
    private const int MaximumSectionLength = 2000;

    public static IReadOnlyList<EditorialKnowledgeChunk> Split(EditorialKnowledgeSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Regex.IsMatch(source.DocumentId, "^[a-z0-9-]{3,64}$", RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(source.Version, "^[0-9]+[.][0-9]+[.][0-9]+$", RegexOptions.CultureInvariant) ||
            string.IsNullOrWhiteSpace(source.Title) || string.IsNullOrWhiteSpace(source.Markdown))
            throw new ArgumentException("شناسه، نسخه یا متن منبع دانش نامعتبر است.", nameof(source));

        var chunks = new List<EditorialKnowledgeChunk>();
        var body = new StringBuilder();
        string? section = null;
        var sectionNumber = 0;

        void AddSection()
        {
            if (section is null) return;
            var content = body.ToString().Trim();
            if (content.Length == 0 || content.Length > MaximumSectionLength)
                throw new ArgumentException("بخش سند باید غیرخالی و حداکثر ۲۰۰۰ نویسه باشد.", nameof(source));
            sectionNumber++;
            var key = $"{source.DocumentId}|{source.Version}|{sectionNumber}";
            var idHash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            var contentHash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            chunks.Add(new(new Guid(idHash.AsSpan(0, 16)), source.DocumentId, source.Version,
                source.Title, section, content, Convert.ToHexString(contentHash).ToLowerInvariant()));
            body.Clear();
        }

        foreach (var line in source.Markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                AddSection();
                section = line[3..].Trim();
                if (section.Length == 0) throw new ArgumentException("عنوان بخش خالی است.", nameof(source));
            }
            else if (section is not null)
            {
                body.AppendLine(line);
            }
        }
        AddSection();
        if (chunks.Count == 0) throw new ArgumentException("سند باید دست‌کم یک عنوان سطح دوم داشته باشد.", nameof(source));
        return chunks;
    }
}
