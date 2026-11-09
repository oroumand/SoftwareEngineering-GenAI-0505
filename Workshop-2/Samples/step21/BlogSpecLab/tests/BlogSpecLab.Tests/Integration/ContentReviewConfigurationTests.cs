using Xunit;
namespace BlogSpecLab.Tests.Integration;
public class ContentReviewConfigurationTests
{
    [Fact] public async Task Malformed_configuration_does_not_crash_startup()
    {
        await using var f = new ContentReviewApiFactory { UseConfiguredGateway = true };
        f.Settings["AI:ContentReview:Enabled"] = "invalid";
        f.Settings["AI:ContentReview:MaxOutputTokenCount"] = "invalid";
        var p = await f.Prepare(); using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        Assert.Equal(200, (int)(await c.GetAsync($"/api/author/posts/{p.Id}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(503, (int)(await c.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken)).StatusCode);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Disabled_or_incomplete_settings_leave_crud_available(bool enabled)
    {
        await using var f = new ContentReviewApiFactory { UseConfiguredGateway = true };
        f.Settings["AI:ContentReview:Enabled"] = enabled.ToString(); f.Settings["AI:ContentReview:Model"] = "";
        var p = await f.Prepare(); using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        Assert.Equal(200, (int)(await c.GetAsync($"/api/author/posts/{p.Id}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(503, (int)(await c.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken)).StatusCode);
        using var other = f.CreateAuthenticatedClient(f.Users.Editors[0]);
        Assert.Equal(403, (int)(await other.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(400, (int)(await c.PostAsync(ContentReviewApiFactory.Route(p.Id), new StringContent("{\"model\":\"other\"}"), TestContext.Current.CancellationToken)).StatusCode);
    }
}
