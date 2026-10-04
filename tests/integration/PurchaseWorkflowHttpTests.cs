using System.Net;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Models;

namespace Stockroom.IntegrationTests;

// Server-side role, ownership, binding, inventory, and dashboard checks over HTTP: T-01 through T-05.
public sealed class PurchaseWorkflowHttpTests : IAsyncLifetime
{
    private readonly StockroomFactory factory = new();
    private int filamentId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.SeedAsync();
        filamentId = (await factory.ItemAsync("FIL-PLA-175")).Id;
    }

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Theory]
    [InlineData("/Inventory")]
    [InlineData("/Requests")]
    [InlineData("/Requests/New")]
    [InlineData("/Requests/Details/1")]
    public async Task ProtectedPagesRequireLogin(string path)
    {
        var response = await factory.CreateBrowserClient().GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Theory]
    [InlineData("filament", true)]
    [InlineData("FILAMENT", true)]
    [InlineData("plywood", false)]
    public async Task InventorySearchMatchesNamesCaseInsensitively(string search, bool expectFilament)
    {
        var client = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);

        var html = await client.GetStringAsync($"/Inventory?q={search}", Ct);

        Assert.Equal(expectFilament, html.Contains("PLA filament, 1.75 mm"));
        Assert.Equal(!expectFilament, html.Contains("Birch plywood sheet, 3 mm"));
    }

    [Fact]
    public async Task InventoryShowsQuantitiesAndEmptyState()
    {
        var client = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);

        var all = await client.GetStringAsync("/Inventory", Ct);
        var none = await client.GetStringAsync("/Inventory?q=zzz", Ct);

        Assert.Matches(@"<td>PLA filament, 1\.75 mm</td>\s*<td>spool</td>\s*<td>2</td>\s*<td>3</td>\s*<td>Low</td>", all);
        Assert.Contains("No items match &quot;zzz&quot;.", none);
        Assert.DoesNotContain("<table>", none);
    }

    [Fact]
    public async Task DashboardListsItemsAtOrBelowThreshold()
    {
        // Filament is below its threshold (2 <= 3); move solder to equal (1 <= 1). Plywood stays above (6 > 2).
        await factory.RunAsync((_, db) => db.InventoryItems.Where(i => i.Sku == "SLD-WIRE-08")
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, 1), Ct));
        var client = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);

        var html = await client.GetStringAsync("/", Ct);

        Assert.Contains("PLA filament, 1.75 mm", html);
        Assert.Contains("Lead-free solder wire, 0.8 mm", html);
        Assert.DoesNotContain("Birch plywood sheet, 3 mm", html);
    }

    [Fact]
    public async Task MemberSubmitsRequestThroughForm()
    {
        var client = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);

        var response = await PostRequestAsync(client, "5", "Materials for the robotics workshop");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = await factory.RunAsync((_, db) => db.PurchaseRequests.SingleAsync(Ct));
        Assert.Equal($"/Requests/Details/{request.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(await factory.UserIdAsync(DemoSeeder.Member1Email), request.RequesterId);
        Assert.Equal(RequestStatus.Pending, request.Status);
    }

    [Theory]
    [InlineData("2.5", "Reason", "is not valid")]
    [InlineData("99999999999", "Reason", "is not valid")]
    [InlineData("0", "Reason", "Enter a whole number from 1 to 10,000.")]
    [InlineData("", "Reason", "Enter a whole number from 1 to 10,000.")]
    [InlineData("5", "  ", "Enter a reason.")]
    public async Task InvalidFormInputShowsFieldErrorAndCreatesNothing(string quantity, string reason, string error)
    {
        var client = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);

        var response = await PostRequestAsync(client, quantity, reason);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(error, await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(0, await factory.RunAsync((_, db) => db.PurchaseRequests.CountAsync(Ct)));
    }

    [Fact]
    public async Task ManagerCannotOpenOrSubmitPurchaseRequest()
    {
        var client = await factory.LoggedInClientAsync(DemoSeeder.ManagerEmail);

        var page = await client.GetAsync("/Requests/New", Ct);
        var post = await PostRequestAsync(client, "5", "Self-approved purchase");

        Assert.Equal("/Account/AccessDenied", page.Headers.Location?.AbsolutePath);
        Assert.Equal("/Account/AccessDenied", post.Headers.Location?.AbsolutePath);
        Assert.Equal(0, await factory.RunAsync((_, db) => db.PurchaseRequests.CountAsync(Ct)));
    }

    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Receive")]
    public async Task MemberPostToManagerActionIsForbiddenAndChangesNothing(string handler)
    {
        var id = await CreateRequestAsync(DemoSeeder.Member1Email);
        if (handler == "Receive")
        {
            var managerId = await factory.UserIdAsync(DemoSeeder.ManagerEmail);
            await factory.RunAsync((s, _) => s.ApproveAsync(id, managerId));
        }
        var before = await factory.SnapshotAsync(id);
        var client = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);

        var response = await StockroomFactory.PostFormAsync(client, $"/Requests/Review/{id}?handler={handler}",
            new() { ["reason"] = "Member attempt" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location?.AbsolutePath);
        Assert.Equal(before, await factory.SnapshotAsync(id));
    }

    [Fact]
    public async Task ManagerWithMemberRoleCannotOpenOrSubmitPurchaseRequest()
    {
        await factory.AddRoleAsync(DemoSeeder.ManagerEmail, Roles.Member);
        var client = await factory.LoggedInClientAsync(DemoSeeder.ManagerEmail);

        var page = await client.GetAsync("/Requests/New", Ct);
        var post = await PostRequestAsync(client, "5", "Self-approved purchase");
        var inventory = await client.GetStringAsync("/Inventory", Ct);

        Assert.Equal("/Account/AccessDenied", page.Headers.Location?.AbsolutePath);
        Assert.Equal("/Account/AccessDenied", post.Headers.Location?.AbsolutePath);
        Assert.DoesNotContain("/Requests/New", inventory);
        Assert.Equal(0, await factory.RunAsync((_, db) => db.PurchaseRequests.CountAsync(Ct)));
    }

    [Fact]
    public async Task MembersSeeOnlyTheirOwnRequests()
    {
        var id = await CreateRequestAsync(DemoSeeder.Member1Email);
        var owner = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);
        var other = await factory.LoggedInClientAsync(DemoSeeder.Member2Email);
        var manager = await factory.LoggedInClientAsync(DemoSeeder.ManagerEmail);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/Requests/Details/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/Requests/Details/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/Requests/Details/{id}", Ct)).StatusCode);
        Assert.Contains($"#{id}", await owner.GetStringAsync("/Requests", Ct));
        Assert.DoesNotContain($"#{id}", await other.GetStringAsync("/Requests", Ct));
        Assert.DoesNotContain($"#{id}", await other.GetStringAsync("/", Ct));
        Assert.Contains($"#{id}", await manager.GetStringAsync("/", Ct));
    }

    [Fact]
    public async Task ManagerApprovesAndReceivesThroughHandlers()
    {
        var id = await CreateRequestAsync(DemoSeeder.Member1Email);
        var manager = await factory.LoggedInClientAsync(DemoSeeder.ManagerEmail);

        await StockroomFactory.PostFormAsync(manager, $"/Requests/Review/{id}?handler=Approve");
        Assert.Equal(2, (await factory.ItemAsync("FIL-PLA-175")).Quantity);
        await StockroomFactory.PostFormAsync(manager, $"/Requests/Review/{id}?handler=Receive");
        var repeat = await StockroomFactory.PostFormAsync(manager, $"/Requests/Review/{id}?handler=Receive");
        var details = await manager.GetStringAsync(repeat.Headers.Location!.OriginalString, Ct);

        Assert.Equal(7, (await factory.ItemAsync("FIL-PLA-175")).Quantity);
        Assert.Contains($"Request #{id} is Received. Nothing was changed.", details);
    }

    private Task<HttpResponseMessage> PostRequestAsync(HttpClient client, string quantity, string reason) =>
        StockroomFactory.PostFormAsync(client, "/Requests/New", new()
        {
            ["Input.ItemId"] = filamentId.ToString(),
            ["Input.Quantity"] = quantity,
            ["Input.Reason"] = reason,
        });

    private async Task<int> CreateRequestAsync(string email)
    {
        var userId = await factory.UserIdAsync(email);
        var result = await factory.RunAsync((s, _) => s.CreateAsync(userId, filamentId, 5, "Materials for the robotics workshop"));
        return result.RequestId!.Value;
    }
}
