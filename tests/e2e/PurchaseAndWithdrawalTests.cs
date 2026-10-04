using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Stockroom.E2ETests;

// Manual demo steps 2 to 8 in a browser: request, approval, receipt, issue, rejected over-issue, and history.
public sealed partial class PurchaseAndWithdrawalTests : BrowserTest
{
    private const string Filament = "PLA filament, 1.75 mm";

    [Fact]
    public async Task PurchaseReceiptAndIssueUpdateStockAndHistory()
    {
        // Member sees two spools and requests five.
        var member = await LogInAsync(StockroomApp.Member1);
        await member.GotoAsync("/Inventory");
        await member.GetByLabel("Search by name").FillAsync("filament");
        await member.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();
        await Expect(InventoryQuantity(member, Filament)).ToHaveTextAsync("2");
        await member.GetByRole(AriaRole.Link, new() { Name = $"Request {Filament}" }).ClickAsync();
        await member.GetByLabel("Quantity").FillAsync("5");
        await member.GetByLabel("Reason").FillAsync("Materials for the robotics workshop");
        await member.GetByRole(AriaRole.Button, new() { Name = "Submit request" }).ClickAsync();
        await Expect(member.GetByRole(AriaRole.Status)).ToHaveTextAsync(RequestSubmitted());
        var requestPath = new Uri(member.Url).AbsolutePath;
        var requestId = requestPath[(requestPath.LastIndexOf('/') + 1)..];

        // Manager approves; approval leaves stock at two.
        var manager = await LogInAsync(StockroomApp.Manager);
        await manager.GotoAsync(requestPath);
        await manager.GetByRole(AriaRole.Button, new() { Name = "Approve request" }).ClickAsync();
        await Expect(manager.GetByRole(AriaRole.Status)).ToHaveTextAsync($"Request #{requestId} approved.");
        await ExpectStockAsync(manager, Filament, "2");

        // Receipt adds the approved quantity once.
        await manager.GotoAsync(requestPath);
        await manager.GetByRole(AriaRole.Button, new() { Name = "Record receipt of 5 spool" }).ClickAsync();
        await Expect(manager.GetByRole(AriaRole.Status)).ToHaveTextAsync($"Request #{requestId} received. Added 5 to stock.");
        await Expect(manager.GetByRole(AriaRole.Button, new() { Name = "Record receipt of 5 spool" })).ToHaveCountAsync(0);
        await ExpectStockAsync(manager, Filament, "7");

        // Issue two spools, then fail to issue six of the remaining five.
        await manager.GetByRole(AriaRole.Link, new() { Name = $"Issue {Filament}" }).ClickAsync();
        await manager.GetByLabel("Quantity to issue").FillAsync("2");
        await manager.GetByLabel("Reason").FillAsync("Robotics workshop prints");
        await manager.GetByRole(AriaRole.Button, new() { Name = "Issue stock" }).ClickAsync();
        await Expect(manager.GetByRole(AriaRole.Status)).ToHaveTextAsync($"Issued 2 spool of {Filament}. 5 remain.");
        await Expect(InventoryQuantity(manager, Filament)).ToHaveTextAsync("5");

        await manager.GetByRole(AriaRole.Link, new() { Name = $"Issue {Filament}" }).ClickAsync();
        await manager.GetByLabel("Quantity to issue").FillAsync("6");
        await manager.GetByLabel("Reason").FillAsync("Open house display");
        await manager.GetByRole(AriaRole.Button, new() { Name = "Issue stock" }).ClickAsync();
        await Expect(manager.GetByRole(AriaRole.Alert)).ToHaveTextAsync($"Only 5 spool of {Filament} in stock. Nothing was changed.");
        await ExpectStockAsync(manager, Filament, "5");

        // Manager history: request decisions and the matching stock movements, newest first.
        await manager.GotoAsync("/History");
        var events = manager.GetByRole(AriaRole.Table).First.GetByRole(AriaRole.Row)
            .Filter(new() { Has = manager.GetByRole(AriaRole.Link, new() { Name = $"#{requestId}", Exact = true }) });
        await Expect(events).ToHaveCountAsync(3);
        await Expect(events.Nth(0)).ToContainTextAsync("Received");
        await Expect(events.Nth(0)).ToContainTextAsync("+5 spool");
        await Expect(events.Nth(1)).ToContainTextAsync("Approved");
        await Expect(events.Nth(2)).ToContainTextAsync("Materials for the robotics workshop");
        var movements = manager.GetByRole(AriaRole.Table).Nth(1).GetByRole(AriaRole.Row);
        await Expect(movements.Nth(1)).ToContainTextAsync("Issue");
        await Expect(movements.Nth(1)).ToContainTextAsync("-2 spool");
        await Expect(movements.Nth(1)).ToContainTextAsync("Robotics workshop prints");
        await Expect(movements.Nth(2)).ToContainTextAsync("Receipt");
        await Expect(movements.Nth(2)).ToContainTextAsync("+5 spool");
        await Expect(manager.GetByText("Open house display")).ToHaveCountAsync(0);
        await Expect(manager.GetByText("UTC").First).ToBeVisibleAsync();

        // The member sees the outcome of their request.
        await member.GotoAsync(requestPath);
        await Expect(member.GetByRole(AriaRole.Heading, new() { Name = "Request history" })).ToBeVisibleAsync();
        await Expect(member.GetByRole(AriaRole.Row).Filter(new() { HasText = "Received" })).ToContainTextAsync("+5 spool");
    }

    [GeneratedRegex(@"^Request #\d+ submitted\.$")]
    private static partial Regex RequestSubmitted();
}
