using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Stockroom.E2ETests;

// T-13: committed stock and history survive an abrupt application restart.
public sealed class PersistenceTests : BrowserTest
{
    private const string Boards = "Microcontroller board, Nano format";

    [Fact]
    public async Task CommittedStockAndHistorySurviveRestart()
    {
        var manager = await LogInAsync(StockroomApp.Manager);
        await manager.GotoAsync("/Inventory");
        await manager.GetByRole(AriaRole.Link, new() { Name = $"Issue {Boards}" }).ClickAsync();
        await manager.GetByLabel("Quantity to issue").FillAsync("2");
        await manager.GetByLabel("Reason").FillAsync("Robotics club kits");
        await manager.GetByRole(AriaRole.Button, new() { Name = "Issue stock" }).ClickAsync();
        await Expect(manager.GetByRole(AriaRole.Status)).ToHaveTextAsync($"Issued 2 boards of {Boards}. 8 boards remain.");

        await App.RestartAsync(Ct);

        var afterRestart = await LogInAsync(StockroomApp.Manager);
        await ExpectStockAsync(afterRestart, Boards, "8");
        await afterRestart.GotoAsync("/History");
        var movement = afterRestart.GetByRole(AriaRole.Row).Filter(new() { HasText = "Robotics club kits" });
        await Expect(movement).ToContainTextAsync("-2 boards");
        await Expect(movement).ToContainTextAsync("manager@stockroom.test");
    }
}
