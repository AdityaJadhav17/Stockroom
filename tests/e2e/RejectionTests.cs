using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Stockroom.E2ETests;

public sealed class RejectionTests : BrowserTest
{
    private const string Filament = "PLA filament, 1.75 mm";

    [Fact]
    public async Task RequesterSeesRejectionReasonAndStockStaysUnchanged()
    {
        var member = await LogInAsync(StockroomApp.Member1);
        await member.GotoAsync("/Inventory");
        await member.GetByRole(AriaRole.Link, new() { Name = $"Request {Filament}" }).ClickAsync();
        await member.GetByLabel("Quantity").FillAsync("3");
        await member.GetByLabel("Reason").FillAsync("Club demo prints");
        await member.GetByRole(AriaRole.Button, new() { Name = "Submit request" }).ClickAsync();
        await Expect(member.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToContainTextAsync("Request #");
        var requestPath = new Uri(member.Url).AbsolutePath;

        var manager = await LogInAsync(StockroomApp.Manager);
        await manager.GotoAsync(requestPath);
        // A blank reason is refused and the request stays reviewable.
        await manager.GetByRole(AriaRole.Button, new() { Name = "Reject request" }).ClickAsync();
        await Expect(manager.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Rejection reason: Enter a reason.");
        await manager.GetByLabel("Rejection reason").FillAsync("Budget closed for September");
        await manager.GetByRole(AriaRole.Button, new() { Name = "Reject request" }).ClickAsync();
        await Expect(manager.GetByRole(AriaRole.Status)).ToContainTextAsync("rejected.");
        await Expect(manager.GetByRole(AriaRole.Button, new() { Name = "Approve request" })).ToHaveCountAsync(0);

        await member.GotoAsync(requestPath);
        await Expect(member.GetByRole(AriaRole.Term).Filter(new() { HasText = "Rejection reason" })).ToBeVisibleAsync();
        await Expect(member.GetByRole(AriaRole.Definition).Filter(new() { HasText = "Budget closed for September" })).ToBeVisibleAsync();
        var rejectedEvent = member.GetByRole(AriaRole.Row).Filter(new() { HasText = "Rejected" });
        await Expect(rejectedEvent).ToContainTextAsync("manager@stockroom.test");
        await Expect(rejectedEvent).ToContainTextAsync("Budget closed for September");
        await ExpectStockAsync(member, Filament, "2");
    }
}
