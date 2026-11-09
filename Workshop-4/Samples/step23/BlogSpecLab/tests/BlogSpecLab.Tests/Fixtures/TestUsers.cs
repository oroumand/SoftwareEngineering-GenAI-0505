using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace BlogSpecLab.Tests.Fixtures;

// فقط دادهٔ fixture؛ نگاشت به مدل و provision پس از T007 و T017 افزوده می‌شود.
public sealed class TestUsers
{
    public IReadOnlyList<TestUser> Authors { get; } =
        Enumerable.Range(0, 5).Select(_ => new TestUser(true, false, false)).ToArray();

    public IReadOnlyList<TestUser> Editors { get; } =
        Enumerable.Range(0, 5).Select(_ => new TestUser(false, true, true)).ToArray();

    public TestUser AuthorEditor { get; } = new(true, true, true);
}

public sealed class TestUser
{
    internal TestUser(bool canWrite, bool canReview, bool canPublish)
    {
        CanWrite = canWrite;
        CanReview = canReview;
        CanPublish = canPublish;
        Credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Credential)));
    }

    public string UserId { get; } = Guid.NewGuid().ToString("N");
    public bool CanWrite { get; }
    public bool CanReview { get; }
    public bool CanPublish { get; }
    public bool IsActive { get; } = true;
    public string TokenHash { get; }

    // مقدار خام فقط در حافظهٔ فرآیند آزمون است؛ در ToString یا خروجی آزمون قرار نمی‌گیرد.
    internal string Credential { get; }

    public void Authenticate(HttpClient client) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Credential);

    public override string ToString() => nameof(TestUser);
}
