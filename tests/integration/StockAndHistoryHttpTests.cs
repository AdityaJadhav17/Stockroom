using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Models;

namespace Stockroom.IntegrationTests;

// Stock-issue pages and history views over HTTP: T-02, T-10, T-13 (US-01, US-06, US-07).
public sealed class StockAndHistoryHttpTests : IAsyncLifetime
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
    [InlineData("/History")]
    [InlineData("/Inventory/Issue/1")]
    public async Task PagesRequireLogin(string path)
    {
        var response = await factory.CreateBrowserClient().GetAsync(path, Ct);

        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task MemberCannotOpenOrPostStockIssue()
    {
        var client = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);
        var before = await FilamentStateAsync();

        var page = await client.GetAsync($"/Inventory/Issue/{filamentId}", Ct);
        var post = await PostIssueAsync(client, "1", "Member attempt");
        var inventory = await client.GetStringAsync("/Inventory", Ct);

        Assert.Equal("/Account/AccessDenied", page.Headers.Location?.AbsolutePath);
        Assert.Equal("/Account/AccessDenied", post.Headers.Location?.AbsolutePath);
        Assert.DoesNotContain("/Inventory/Issue/", inventory);
        Assert.Equal(before, await FilamentStateAsync());
    }

    [Fact]
    public async Task ManagerIssuesStockFromInventory()
    {
        var client = await factory.LoggedInClientAsync(DemoSeeder.ManagerEmail);

        var inventory = await client.GetStringAsync("/Inventory", Ct);
        var post = await PostIssueAsync(client, "2", "Laser cutter workshop");
        var after = await client.GetStringAsync(post.Headers.Location!.OriginalString, Ct);

        Assert.Contains($"/Inventory/Issue/{filamentId}", inventory);
        Assert.Equal("/Inventory", post.Headers.Location?.OriginalString);
        Assert.Contains("Issued 2 spools of PLA filament, 1.75 mm. 0 spools remain.", after);
        Assert.Equal("0|11", await FilamentStateAsync());
    }

    [Theory]
    [InlineData("1.5", "Workshop", "is not valid")]
    [InlineData("99999999999", "Workshop", "is not valid")]
    [InlineData("0", "Workshop", "Enter a whole number from 1 to 10,000.")]
    [InlineData("", "Workshop", "Enter a whole number from 1 to 10,000.")]
    [InlineData("1", "  ", "Enter a reason.")]
    [InlineData("3", "Workshop", "Only 2 spools of PLA filament, 1.75 mm in stock. Nothing was changed.")]
    public async Task InvalidOrExcessiveIssueShowsErrorAndChangesNothing(string quantity, string reason, string error)
    {
        var client = await factory.LoggedInClientAsync(DemoSeeder.ManagerEmail);
        var before = await FilamentStateAsync();

        var response = await PostIssueAsync(client, quantity, reason);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(error, await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(before, await FilamentStateAsync());
    }

    [Fact]
    public async Task ManagerHistoryShowsRequestEventsAndMovements()
    {
        var requestId = await CreateApprovedRequestAsync(DemoSeeder.Member1Email);
        var client = await factory.LoggedInClientAsync(DemoSeeder.ManagerEmail);
        await PostIssueAsync(client, "1", "Laser cutter workshop");

        var html = await client.GetStringAsync("/History", Ct);

        Assert.Contains("Purchase request events", html);
        Assert.Matches($@"<td><time datetime=""\d{{4}}-\d{{2}}-\d{{2}}T\d{{2}}:\d{{2}}:\d{{2}}Z"">\d{{4}}-\d{{2}}-\d{{2}} \d{{2}}:\d{{2}} UTC</time></td>\s*<td>Approved</td>\s*<td><a href=""/Requests/Details/{requestId}"">#{requestId}</a></td>", html);
        Assert.Contains("Stock movements", html);
        Assert.Matches(@"<td>Issue</td>\s*<td>PLA filament, 1\.75 mm</td>\s*<td>-1 spool</td>\s*<td>manager@stockroom\.test</td>\s*<td>Laser cutter workshop</td>", html);
        Assert.Contains("<td>Opening</td>", html);
        // Deterministic order: the newest movement (the issue) appears before the opening movements.
        Assert.True(html.IndexOf("<td>Issue</td>", StringComparison.Ordinal) < html.IndexOf("<td>Opening</td>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MemberHistoryShowsOnlyOwnRequestEvents()
    {
        var own = await CreateApprovedRequestAsync(DemoSeeder.Member1Email);
        var other = await CreateApprovedRequestAsync(DemoSeeder.Member2Email);
        var client = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);

        var html = await client.GetStringAsync("/History", Ct);
        var details = await client.GetStringAsync($"/Requests/Details/{own}", Ct);

        Assert.Contains("Your request events", html);
        Assert.Contains($"#{own}</a>", html);
        Assert.DoesNotContain($"#{other}</a>", html);
        Assert.DoesNotContain("Stock movements", html);
        Assert.DoesNotContain("<td>Opening</td>", html);
        Assert.Contains("Request history", details);
        Assert.Matches(@"<td>Approved</td>[\s\S]*<td>Created</td>", details);
        Assert.Matches(@"<td>Created</td>[\s\S]*?<td>Robotics workshop</td>\s*</tr>", html);
    }

    [Fact]
    public async Task HistoryShowsEmptyStateAndOffersNoEditOrDelete()
    {
        var client = await factory.LoggedInClientAsync(DemoSeeder.Member2Email);

        var html = await client.GetStringAsync("/History", Ct);

        Assert.Contains("No request events yet.", html);
        // The logout form is the only POST form on the page.
        Assert.Single(Regex.Matches(html, "method=\"post\""));
    }

    private Task<HttpResponseMessage> PostIssueAsync(HttpClient client, string quantity, string reason) =>
        StockroomFactory.PostFormAsync(client, $"/Inventory/Issue/{filamentId}", new()
        {
            ["Input.Quantity"] = quantity,
            ["Input.Reason"] = reason,
        });

    private async Task<int> CreateApprovedRequestAsync(string email)
    {
        var userId = await factory.UserIdAsync(email);
        var managerId = await factory.UserIdAsync(DemoSeeder.ManagerEmail);
        var id = (await factory.RunAsync((s, _) => s.CreateAsync(userId, filamentId, 5, "Robotics workshop"))).RequestId!.Value;
        await factory.RunAsync((s, _) => s.ApproveAsync(id, managerId));
        return id;
    }

    private Task<string> FilamentStateAsync() =>
        factory.RunAsync(async (_, db) =>
            $"{await db.InventoryItems.Where(i => i.Id == filamentId).Select(i => i.Quantity).SingleAsync(Ct)}|" +
            $"{await db.StockMovements.CountAsync(Ct)}");
}
