using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Models;
using Stockroom.Web.Services;

namespace Stockroom.IntegrationTests;

// Stock issues and history against SQLite: T-02, T-10 through T-13 (US-06, US-07).
public sealed class StockServiceTests : IAsyncLifetime
{
    private const string Filament = "FIL-PLA-175";
    private const string Boards = "MCU-NANO";

    private readonly StockroomFactory factory = new();
    private string memberId = "";
    private string managerId = "";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.SeedAsync();
        memberId = await factory.UserIdAsync(DemoSeeder.Member1Email);
        managerId = await factory.UserIdAsync(DemoSeeder.ManagerEmail);
    }

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task ValidIssueReducesStockAndRecordsNegativeMovement()
    {
        var boards = await factory.ItemAsync(Boards);
        var before = DateTime.UtcNow;

        var result = await factory.StockAsync(s => s.IssueAsync(boards.Id, managerId, 3, "  Workshop kits  "));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(7, (await factory.ItemAsync(Boards)).Quantity);
        var movement = Assert.Single(await IssuesAsync());
        Assert.Equal((boards.Id, -3, managerId, "Workshop kits", (int?)null),
            (movement.ItemId, movement.QuantityDelta, movement.ActorId, movement.Reason, movement.PurchaseRequestId));
        Assert.InRange(movement.OccurredAtUtc, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task IssueOfAllRemainingStockLeavesZero()
    {
        var filament = await factory.ItemAsync(Filament);

        var result = await factory.StockAsync(s => s.IssueAsync(filament.Id, managerId, 2, "Workshop"));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(0, (await factory.ItemAsync(Filament)).Quantity);
    }

    public static TheoryData<int?, string?> InvalidIssues => new()
    {
        { 0, "Reason" },
        { -1, "Reason" },
        { null, "Reason" },
        { PurchaseRules.MaxQuantity + 1, "Reason" },
        { 1, null },
        { 1, "   " },
        { 1, new string('x', PurchaseRules.MaxReasonLength + 1) },
    };

    [Theory]
    [MemberData(nameof(InvalidIssues))]
    public async Task InvalidIssueChangesNothing(int? quantity, string? reason)
    {
        var boards = await factory.ItemAsync(Boards);
        var before = await StateAsync();

        var result = await factory.StockAsync(s => s.IssueAsync(boards.Id, managerId, quantity, reason));

        Assert.False(result.Succeeded);
        Assert.Equal(before, await StateAsync());
    }

    [Fact]
    public async Task IssueExceedingStockReportsShortageAndChangesNothing()
    {
        var filament = await factory.ItemAsync(Filament);
        var before = await StateAsync();

        var result = await factory.StockAsync(s => s.IssueAsync(filament.Id, managerId, 3, "Workshop"));

        Assert.False(result.Succeeded);
        Assert.Equal("Only 2 spool of PLA filament, 1.75 mm in stock. Nothing was changed.", result.Message);
        Assert.Equal(before, await StateAsync());
    }

    [Fact]
    public async Task UnknownItemChangesNothing()
    {
        var before = await StateAsync();

        var result = await factory.StockAsync(s => s.IssueAsync(9999, managerId, 1, "Workshop"));

        Assert.False(result.Succeeded);
        Assert.Equal(before, await StateAsync());
    }

    [Fact]
    public async Task MemberCannotIssueThroughService()
    {
        var boards = await factory.ItemAsync(Boards);
        var before = await StateAsync();

        var result = await factory.StockAsync(s => s.IssueAsync(boards.Id, memberId, 1, "Member attempt"));

        Assert.Equal(ServiceGuard.ForbiddenMessage, result.Message);
        Assert.Equal(before, await StateAsync());
    }

    [Fact]
    public async Task CompetingOneUnitIssuesCannotOversell()
    {
        // T-12: one spool left and two simultaneous one-spool issues on separate connections to one file.
        var filament = await factory.ItemAsync(Filament);
        await factory.RunAsync((_, db) => db.InventoryItems.Where(i => i.Id == filament.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, 1), Ct));
        using var start = new Barrier(2);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
            factory.StockAsync(s => { start.SignalAndWait(Ct); return s.IssueAsync(filament.Id, managerId, 1, "Workshop"); }), Ct)));

        Assert.Single(results, r => r.Succeeded);
        Assert.Contains("Only 0 spool", Assert.Single(results, r => !r.Succeeded).Message);
        Assert.Equal(0, (await factory.ItemAsync(Filament)).Quantity);
        Assert.Single(await IssuesAsync());
    }

    [Fact]
    public async Task FailedMovementWriteRollsBackStockChange()
    {
        // T-11: the conditional stock update succeeds, then the movement insert fails.
        var boards = await factory.ItemAsync(Boards);
        await factory.RunAsync((_, db) => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER fail_issue_movement BEFORE INSERT ON StockMovements WHEN NEW.Action = 'Issue' " +
            "BEGIN SELECT RAISE(ABORT, 'forced failure'); END;", Ct));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            factory.StockAsync(s => s.IssueAsync(boards.Id, managerId, 3, "Workshop kits")));

        Assert.Equal(10, (await factory.ItemAsync(Boards)).Quantity);
        Assert.Empty(await IssuesAsync());
    }

    [Fact]
    public async Task MovementHistoryIsEmptyForMembers()
    {
        var boards = await factory.ItemAsync(Boards);
        await factory.StockAsync(s => s.IssueAsync(boards.Id, managerId, 1, "Workshop"));

        var managerView = await factory.StockAsync(s => s.MovementHistory(managerId).ToListAsync(Ct));
        var memberView = await factory.StockAsync(s => s.MovementHistory(memberId).ToListAsync(Ct));

        Assert.Equal(11, managerView.Count);
        Assert.Equal((StockAction.Issue, -1, DemoSeeder.ManagerEmail), (managerView[0].Action, managerView[0].QuantityDelta, managerView[0].ActorEmail));
        Assert.Empty(memberView);
    }

    [Fact]
    public async Task CommittedStockAndHistoryPersistAfterReopeningDatabase()
    {
        // T-13: read through a new, unpooled connection after the application's connections close.
        var boards = await factory.ItemAsync(Boards);
        var filament = await factory.ItemAsync(Filament);
        await factory.StockAsync(s => s.IssueAsync(boards.Id, managerId, 2, "Workshop kits"));
        var requestId = (await factory.RunAsync((s, _) => s.CreateAsync(memberId, filament.Id, 5, "Robotics workshop"))).RequestId!.Value;
        await factory.RunAsync((s, _) => s.ApproveAsync(requestId, managerId));
        await factory.RunAsync((s, _) => s.ReceiveAsync(requestId, managerId));
        SqliteConnection.ClearAllPools();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={factory.DatabasePath};Pooling=False").Options;
        await using var reopened = new AppDbContext(options);

        Assert.Equal(8, (await reopened.InventoryItems.SingleAsync(i => i.Sku == Boards, Ct)).Quantity);
        Assert.Equal(7, (await reopened.InventoryItems.SingleAsync(i => i.Sku == Filament, Ct)).Quantity);
        Assert.Equal(1, await reopened.StockMovements.CountAsync(m => m.Action == StockAction.Issue && m.Reason == "Workshop kits", Ct));
        Assert.Equal(1, await reopened.StockMovements.CountAsync(m => m.Action == StockAction.Receipt && m.PurchaseRequestId == requestId, Ct));
        Assert.Equal(
            [RequestAction.Created, RequestAction.Approved, RequestAction.Received],
            await reopened.RequestEvents.Where(e => e.PurchaseRequestId == requestId).OrderBy(e => e.Id).Select(e => e.Action).ToListAsync(Ct));
    }

    private Task<string> StateAsync() =>
        factory.RunAsync(async (_, db) =>
        {
            var quantities = await db.InventoryItems.OrderBy(i => i.Id).Select(i => i.Quantity).ToListAsync(Ct);
            var movements = await db.StockMovements.CountAsync(Ct);
            return $"{string.Join(",", quantities)}|{movements}";
        });

    private Task<List<StockMovement>> IssuesAsync() =>
        factory.RunAsync((_, db) => db.StockMovements.Where(m => m.Action == StockAction.Issue).ToListAsync(Ct));
}
