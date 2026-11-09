using BlogSpecLab.Core.Application.EditorialKnowledge;
using Xunit;

namespace BlogSpecLab.Tests.Application;

public class EditorialKnowledgeChunkerTests
{
    private static EditorialKnowledgeSource Source(string version = "1.0.0") =>
        new("editorial-policy", version, "سیاست آموزشی",
            "# عنوان سند\n\n## عنوان‌گذاری\nقاعدهٔ عنوان روشن.\n\n## آمار\nآمار نیازمند منبع است.");

    [Fact]
    public void Splits_by_semantic_sections_and_produces_stable_point_ids()
    {
        var first = MarkdownSectionChunker.Split(Source());
        var again = MarkdownSectionChunker.Split(Source());
        Assert.Equal(2, first.Count);
        Assert.Equal(first.Select(x => x.Id), again.Select(x => x.Id));
        Assert.Equal("عنوان‌گذاری", first[0].Section);
        Assert.Contains("قاعدهٔ عنوان", first[0].Content);
        Assert.DoesNotContain("آمار نیازمند منبع", first[0].Content);
        Assert.Equal(64, first[0].ContentSha256.Length);
        Assert.NotEqual(first[0].Id, MarkdownSectionChunker.Split(Source("1.0.1"))[0].Id);
    }

    [Theory]
    [InlineData("# بدون بخش", "1.0.0")]
    [InlineData("## بخش تهی", "1.0.0")]
    [InlineData("## عنوان\nمتن", "invalid")]
    public void Invalid_source_fails_before_embedding(string markdown, string version)
    {
        Assert.Throws<ArgumentException>(() =>
            MarkdownSectionChunker.Split(new("editorial-policy", version, "سیاست", markdown)));
    }

    [Fact]
    public void Oversized_section_is_not_silently_truncated()
    {
        var document = new EditorialKnowledgeSource("editorial-policy", "1.0.0", "سیاست",
            "## آمار\n" + new string('الف', 2100));
        Assert.Throws<ArgumentException>(() => MarkdownSectionChunker.Split(document));
    }
}
