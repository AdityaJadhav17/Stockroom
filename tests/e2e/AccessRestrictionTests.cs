using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Stockroom.E2ETests;

// Seeded requests: #1 and #4 belong to member1; #2 and #3 belong to member2.
public sealed class AccessRestrictionTests : BrowserTest
{
    [Fact]
    public async Task MemberCannotOpenIssuePageOrAnotherMembersRequest()
    {
        var member = await LogInAsync(StockroomApp.Member1);

        await member.GotoAsync("/Inventory");
        await Expect(member.GetByRole(AriaRole.Link, new() { Name = "Issue", Exact = false })).ToHaveCountAsync(0);
        await member.GotoAsync("/Inventory/Issue/1");
        await Expect(member.GetByRole(AriaRole.Heading, new() { Name = "Access denied" })).ToBeVisibleAsync();

        var otherRequest = await member.GotoAsync("/Requests/Details/2");
        Assert.Equal(404, otherRequest!.Status);
        var ownRequest = await member.GotoAsync("/Requests/Details/1");
        Assert.Equal(200, ownRequest!.Status);
    }

    [Fact]
    public async Task MemberHistoryShowsOnlyTheirOwnRequests()
    {
        var member = await LogInAsync(StockroomApp.Member1);

        await member.GotoAsync("/History");

        await Expect(member.GetByRole(AriaRole.Heading, new() { Name = "Your request events" })).ToBeVisibleAsync();
        await Expect(member.GetByRole(AriaRole.Link, new() { Name = "#1", Exact = true }).First).ToBeVisibleAsync();
        await Expect(member.GetByRole(AriaRole.Link, new() { Name = "#4", Exact = true })).ToHaveCountAsync(1);
        await Expect(member.GetByRole(AriaRole.Link, new() { Name = "#2", Exact = true })).ToHaveCountAsync(0);
        await Expect(member.GetByRole(AriaRole.Link, new() { Name = "#3", Exact = true })).ToHaveCountAsync(0);
        await Expect(member.GetByRole(AriaRole.Heading, new() { Name = "Stock movements" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task LogoutBlocksProtectedPages()
    {
        var member = await LogInAsync(StockroomApp.Member1);

        await member.GetByRole(AriaRole.Button, new() { Name = "Log out" }).ClickAsync();
        await Expect(member.GetByRole(AriaRole.Heading, new() { Name = "Log in" })).ToBeVisibleAsync();

        foreach (var path in new[] { "/History", "/Inventory", "/Requests/Details/1" })
        {
            await member.GotoAsync(path);
            await Expect(member).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Account/Login"));
            await Expect(member.GetByRole(AriaRole.Heading, new() { Name = "Log in" })).ToBeVisibleAsync();
        }
    }
}
