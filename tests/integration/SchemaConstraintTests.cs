using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockroom.Web.Data;
using Stockroom.Web.Models;

namespace Stockroom.IntegrationTests;

// SQLite CHECK constraints and indexes on stock movements (BR-05, BR-10, BR-11).
public sealed class SchemaConstraintTests : IAsyncLifetime
{
    private readonly StockroomFactory factory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.SeedAsync();

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task IssueRequiresNonblankReason(string? reason) =>
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            SaveMovementAsync(StockAction.Issue, -1, reason, requestId: null));

    [Fact]
    public async Task IssueRejectsReasonOver500Characters() =>
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            SaveMovementAsync(StockAction.Issue, -1, new string('x', 501), requestId: null));

    [Fact]
    public async Task IssueAcceptsReasonOf500Characters() =>
        await SaveMovementAsync(StockAction.Issue, -1, new string('x', 500), requestId: null);

    [Fact]
    public async Task ReceiptRequiresPurchaseRequest() =>
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            SaveMovementAsync(StockAction.Receipt, 5, null, requestId: null));

    [Fact]
    public async Task SecondReceiptForSameRequestIsRejected()
    {
        var requestId = await CreateRequestAsync();
        await SaveMovementAsync(StockAction.Receipt, 5, null, requestId);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            SaveMovementAsync(StockAction.Receipt, 5, null, requestId));
    }

    private async Task SaveMovementAsync(StockAction action, int delta, string? reason, int? requestId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.InventoryItems.SingleAsync(i => i.Sku == "FIL-PLA-175", Ct);
        var manager = await db.Users.SingleAsync(u => u.Email == DemoSeeder.ManagerEmail, Ct);
        db.StockMovements.Add(new StockMovement
        {
            ItemId = item.Id,
            QuantityDelta = delta,
            Action = action,
            ActorId = manager.Id,
            OccurredAtUtc = DateTime.UtcNow,
            Reason = reason,
            PurchaseRequestId = requestId,
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<int> CreateRequestAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.InventoryItems.SingleAsync(i => i.Sku == "FIL-PLA-175", Ct);
        var member = await db.Users.SingleAsync(u => u.Email == DemoSeeder.Member1Email, Ct);
        var request = new PurchaseRequest
        {
            ItemId = item.Id,
            Quantity = 5,
            Reason = "Materials for the robotics workshop",
            RequesterId = member.Id,
            Status = RequestStatus.Approved,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.PurchaseRequests.Add(request);
        await db.SaveChangesAsync(Ct);
        return request.Id;
    }
}
