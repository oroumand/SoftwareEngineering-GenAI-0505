using System.Text;
using BlogSpecLab.Application.Posts.Ports;

namespace BlogSpecLab.Api.Posts.Mapping;

public static class ETagMapping
{
    public static string ToETag(PostVersion version) => $"\"{Convert.ToBase64String(Encoding.UTF8.GetBytes(version.Value))}\"";

    public static bool TryToPostVersion(string? etag, out PostVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(etag) || etag.Length < 2 || etag[0] != '\"' || etag[^1] != '\"')
        {
            return false;
        }

        try
        {
            var value = Encoding.UTF8.GetString(Convert.FromBase64String(etag[1..^1]));
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            version = new PostVersion(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
