using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Infra.DataAccess.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace BlogSpecLab.Tests.Integration;
public class ContentReviewConcurrencyTests
{
    [Fact] public async Task Storage_failure_in_recheck_suppresses_even_valid_report()
    {
        await using var f = new ContentReviewApiFactory(); var p = await f.Prepare(); f.Gateway.Pause = true;
        using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        var pending = c.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken);
        await f.Gateway.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await using var db = Us3Support.Context(f);
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(db.Database,
            "ALTER TABLE UserAccess RENAME TO MissingUserAccess", TestContext.Current.CancellationToken);
        f.Gateway.Release.TrySetResult();
        var response = await pending; Assert.Equal(503, (int)response.StatusCode);
        Assert.Equal("storage_unavailable", (await ContentReviewApiTests.Json(response)).GetProperty("code").GetString());
    }
    [Theory] [InlineData("edit", 200, false)] [InlineData("restore", 200, false)]
    [InlineData("submit", 200, true)] [InlineData("delete", 404, false)] [InlineData("permission", 403, false)]
    public async Task Independent_changes_preserved_and_freshness_uses_revision(string change, int status, bool fresh)
    {
        await using var f = new ContentReviewApiFactory(); var p = await f.Prepare(); f.Gateway.Pause = true;
        using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]); var pending = c.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken);
        await f.Gateway.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (change == "permission")
        {
            await using var scope = f.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AccessProvisioner>().RevokePermissionsAsync(f.Users.Authors[0].UserId, TestContext.Current.CancellationToken);
        }
        else await f.Change(p.Id, post => {
            if (change == "delete") post.Delete(); else if (change == "submit") post.Submit();
            else { post.Edit("جدید", "متن جدید"); if (change == "restore") post.Edit(p.Title, p.Body); }
        });
        f.Gateway.Release.TrySetResult(); var response = await pending; Assert.Equal(status, (int)response.StatusCode);
        if (status == 200)
        {
            var json = await ContentReviewApiTests.Json(response);
            Assert.Equal(1, json.GetProperty("source").GetProperty("contentRevision").GetInt32());
            Assert.Equal(fresh, json.GetProperty("freshness").GetProperty("isCurrentContent").GetBoolean());
        }
    }
}
