using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Models;
using Stockroom.Web.Services;

namespace Stockroom.IntegrationTests;

// Purchase workflow rules against SQLite: T-05 through T-11 (US-03, US-04, US-05).
public sealed class PurchaseServiceTests : IAsyncLifetime
{
    private const string Filament = "FIL-PLA-175";

    private readonly StockroomFactory factory = new();
    private string memberId = "";
    private string managerId = "";
    private int filamentId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.SeedAsync();
        memberId = await factory.UserIdAsync(DemoSeeder.Member1Email);
        managerId = await factory.UserIdAsync(DemoSeeder.ManagerEmail);
        filamentId = (await factory.ItemAsync(Filament)).Id;
    }

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task ValidRequestIsPendingWithRequesterTimeAndEvent()
    {
        var before = DateTime.UtcNow;

        var result = await factory.RunAsync((s, _) => s.CreateAsync(memberId, filamentId, 5, "  Materials for the robotics workshop  "));

        Assert.True(result.Succeeded, result.Message);
        var request = await factory.RunAsync((_, db) => db.PurchaseRequests.SingleAsync(Ct));
        Assert.Equal(RequestStatus.Pending, request.Status);
        Assert.Equal(memberId, request.RequesterId);
        Assert.Equal(5, request.Quantity);
        Assert.Equal("Materials for the robotics workshop", request.Reason);
        Assert.InRange(request.CreatedAtUtc, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
        Assert.Equal([RequestAction.Created], await EventsAsync(request.Id));
    }

    public static TheoryData<int?, string?> InvalidRequests => new()
    {
        { 0, "Reason" },
        { -1, "Reason" },
        { PurchaseRules.MaxQuantity + 1, "Reason" },
        { null, "Reason" },
        { 5, null },
        { 5, "   " },
        { 5, new string('x', PurchaseRules.MaxReasonLength + 1) },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequestCreatesNothing(int? quantity, string? reason)
    {
        var result = await factory.RunAsync((s, _) => s.CreateAsync(memberId, filamentId, quantity, reason));

        Assert.False(result.Succeeded);
        Assert.Equal(0, await factory.RunAsync((_, db) => db.PurchaseRequests.CountAsync(Ct)));
        Assert.Equal(0, await factory.RunAsync((_, db) => db.RequestEvents.CountAsync(Ct)));
    }

    [Fact]
    public async Task UnknownItemCreatesNothing()
    {
        var result = await factory.RunAsync((s, _) => s.CreateAsync(memberId, 9999, 5, "Reason"));

        Assert.False(result.Succeeded);
        Assert.Equal(0, await factory.RunAsync((_, db) => db.PurchaseRequests.CountAsync(Ct)));
    }

    [Fact]
    public async Task ManagerCannotCreatePurchase()
    {
        var result = await factory.RunAsync((s, _) => s.CreateAsync(managerId, filamentId, 5, "Self-approved purchase"));

        Assert.Equal(PurchaseService.ForbiddenMessage, result.Message);
        Assert.Equal(0, await factory.RunAsync((_, db) => db.PurchaseRequests.CountAsync(Ct)));
    }

    [Fact]
    public async Task ManagerWithMemberRoleCannotCreatePurchase()
    {
        await factory.AddRoleAsync(DemoSeeder.ManagerEmail, Roles.Member);

        var result = await factory.RunAsync((s, _) => s.CreateAsync(managerId, filamentId, 5, "Self-approved purchase"));

        Assert.Equal(PurchaseService.ForbiddenMessage, result.Message);
        Assert.Equal(0, await factory.RunAsync((_, db) => db.PurchaseRequests.CountAsync(Ct)));
    }

    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Receive")]
    public async Task MemberCannotPerformManagerActionsThroughService(string action)
    {
        var id = action == "Receive" ? await CreateApprovedAsync() : await CreatePendingAsync();
        var before = await factory.SnapshotAsync(id);

        var result = await factory.RunAsync((s, _) => action switch
        {
            "Approve" => s.ApproveAsync(id, memberId),
            "Reject" => s.RejectAsync(id, memberId, "Member attempt"),
            _ => s.ReceiveAsync(id, memberId),
        });

        Assert.Equal(PurchaseService.ForbiddenMessage, result.Message);
        Assert.Equal(before, await factory.SnapshotAsync(id));
    }

    [Fact]
    public async Task ApprovalRecordsReviewerAndLeavesStockUnchanged()
    {
        var id = await CreatePendingAsync();

        var result = await factory.RunAsync((s, _) => s.ApproveAsync(id, managerId));

        Assert.True(result.Succeeded, result.Message);
        var request = await RequestAsync(id);
        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Equal(managerId, request.ReviewerId);
        Assert.NotNull(request.ReviewedAtUtc);
        Assert.Equal(2, (await factory.ItemAsync(Filament)).Quantity);
        Assert.Equal([RequestAction.Created, RequestAction.Approved], await EventsAsync(id));
    }

    [Fact]
    public async Task RejectionRecordsReviewerAndReason()
    {
        var id = await CreatePendingAsync();

        var result = await factory.RunAsync((s, _) => s.RejectAsync(id, managerId, "  Budget closed  "));

        Assert.True(result.Succeeded, result.Message);
        var request = await RequestAsync(id);
        Assert.Equal(RequestStatus.Rejected, request.Status);
        Assert.Equal(managerId, request.ReviewerId);
        Assert.Equal("Budget closed", request.RejectionReason);
        Assert.Equal([RequestAction.Created, RequestAction.Rejected], await EventsAsync(id));
    }

    public static TheoryData<string?> InvalidReasons => new() { (string?)null, "   ", new string('x', PurchaseRules.MaxReasonLength + 1) };

    [Theory]
    [MemberData(nameof(InvalidReasons))]
    public async Task InvalidRejectionReasonLeavesRequestPending(string? reason)
    {
        var id = await CreatePendingAsync();

        var result = await factory.RunAsync((s, _) => s.RejectAsync(id, managerId, reason));

        Assert.False(result.Succeeded);
        Assert.Equal(RequestStatus.Pending, (await RequestAsync(id)).Status);
        Assert.Equal([RequestAction.Created], await EventsAsync(id));
    }

    [Fact]
    public async Task RepeatedReviewReportsCurrentStatusAndChangesNothing()
    {
        var id = await CreatePendingAsync();
        await factory.RunAsync((s, _) => s.ApproveAsync(id, managerId));

        var again = await factory.RunAsync((s, _) => s.ApproveAsync(id, managerId));
        var reject = await factory.RunAsync((s, _) => s.RejectAsync(id, managerId, "Too late"));

        Assert.False(again.Succeeded);
        Assert.Contains("is Approved", again.Message);
        Assert.False(reject.Succeeded);
        var request = await RequestAsync(id);
        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Null(request.RejectionReason);
        Assert.Equal([RequestAction.Created, RequestAction.Approved], await EventsAsync(id));
    }

    [Fact]
    public async Task CompetingApprovalAndRejectionCommitOneDecision()
    {
        var id = await CreatePendingAsync();
        using var start = new Barrier(2);

        var results = await Task.WhenAll(
            Task.Run(() => factory.RunAsync((s, _) => { start.SignalAndWait(Ct); return s.ApproveAsync(id, managerId); }), Ct),
            Task.Run(() => factory.RunAsync((s, _) => { start.SignalAndWait(Ct); return s.RejectAsync(id, managerId, "Duplicate"); }), Ct));

        Assert.Single(results, r => r.Succeeded);
        var request = await RequestAsync(id);
        Assert.NotEqual(RequestStatus.Pending, request.Status);
        Assert.Single(await EventsAsync(id), a => a is RequestAction.Approved or RequestAction.Rejected);
    }

    [Fact]
    public async Task ReceiptAddsApprovedQuantityWithMovementAndEvent()
    {
        var id = await CreateApprovedAsync();

        var result = await factory.RunAsync((s, _) => s.ReceiveAsync(id, managerId));

        Assert.True(result.Succeeded, result.Message);
        var request = await RequestAsync(id);
        Assert.Equal(RequestStatus.Received, request.Status);
        Assert.Equal(managerId, request.ReceiverId);
        Assert.NotNull(request.ReceivedAtUtc);
        Assert.Equal(7, (await factory.ItemAsync(Filament)).Quantity);
        var movement = Assert.Single(await ReceiptMovementsAsync());
        Assert.Equal((5, id, managerId), (movement.QuantityDelta, movement.PurchaseRequestId, movement.ActorId));
        Assert.Equal([RequestAction.Created, RequestAction.Approved, RequestAction.Received], await EventsAsync(id));
    }

    [Fact]
    public async Task PendingAndRejectedRequestsCannotBeReceived()
    {
        var pending = await CreatePendingAsync();
        var rejected = await CreatePendingAsync();
        await factory.RunAsync((s, _) => s.RejectAsync(rejected, managerId, "Not needed"));

        var results = new[]
        {
            await factory.RunAsync((s, _) => s.ReceiveAsync(pending, managerId)),
            await factory.RunAsync((s, _) => s.ReceiveAsync(rejected, managerId)),
        };

        Assert.All(results, r => Assert.False(r.Succeeded));
        Assert.Equal(RequestStatus.Pending, (await RequestAsync(pending)).Status);
        Assert.Equal(RequestStatus.Rejected, (await RequestAsync(rejected)).Status);
        Assert.Equal(2, (await factory.ItemAsync(Filament)).Quantity);
        Assert.Empty(await ReceiptMovementsAsync());
    }

    [Fact]
    public async Task RepeatedReceiptAddsStockOnce()
    {
        var id = await CreateApprovedAsync();
        await factory.RunAsync((s, _) => s.ReceiveAsync(id, managerId));

        var again = await factory.RunAsync((s, _) => s.ReceiveAsync(id, managerId));

        Assert.False(again.Succeeded);
        Assert.Contains("is Received", again.Message);
        Assert.Equal(7, (await factory.ItemAsync(Filament)).Quantity);
        Assert.Single(await ReceiptMovementsAsync());
        Assert.Single(await EventsAsync(id), a => a == RequestAction.Received);
    }

    [Fact]
    public async Task CompetingReceiptsAddStockOnce()
    {
        const int competitors = 4;
        var id = await CreateApprovedAsync();
        using var start = new Barrier(competitors);

        var results = await Task.WhenAll(Enumerable.Range(0, competitors).Select(_ => Task.Run(() =>
            factory.RunAsync((s, _) => { start.SignalAndWait(Ct); return s.ReceiveAsync(id, managerId); }), Ct)));

        Assert.Single(results, r => r.Succeeded);
        Assert.Equal(7, (await factory.ItemAsync(Filament)).Quantity);
        Assert.Single(await ReceiptMovementsAsync());
        Assert.Single(await EventsAsync(id), a => a == RequestAction.Received);
    }

    [Fact]
    public async Task ReceiptThatWouldOverflowStockChangesNothing()
    {
        var id = await CreateApprovedAsync();
        await factory.RunAsync((_, db) => db.InventoryItems.Where(i => i.Id == filamentId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, int.MaxValue - 2), Ct));

        var result = await factory.RunAsync((s, _) => s.ReceiveAsync(id, managerId));

        Assert.False(result.Succeeded);
        Assert.Equal(RequestStatus.Approved, (await RequestAsync(id)).Status);
        Assert.Equal(int.MaxValue - 2, (await factory.ItemAsync(Filament)).Quantity);
        Assert.Empty(await ReceiptMovementsAsync());
        Assert.DoesNotContain(RequestAction.Received, await EventsAsync(id));
    }

    [Fact]
    public async Task FailedHistoryWriteRollsBackReceipt()
    {
        var id = await CreateApprovedAsync();
        // T-11: force the request-event insert to fail after the status and stock writes.
        await factory.RunAsync((_, db) => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER fail_receipt_event BEFORE INSERT ON RequestEvents WHEN NEW.Action = 'Received' " +
            "BEGIN SELECT RAISE(ABORT, 'forced failure'); END;", Ct));

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.RunAsync((s, _) => s.ReceiveAsync(id, managerId)));

        var request = await RequestAsync(id);
        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Null(request.ReceiverId);
        Assert.Equal(2, (await factory.ItemAsync(Filament)).Quantity);
        Assert.Empty(await ReceiptMovementsAsync());
    }

    private async Task<int> CreatePendingAsync() =>
        (await factory.RunAsync((s, _) => s.CreateAsync(memberId, filamentId, 5, "Materials for the robotics workshop"))).RequestId!.Value;

    private async Task<int> CreateApprovedAsync()
    {
        var id = await CreatePendingAsync();
        await factory.RunAsync((s, _) => s.ApproveAsync(id, managerId));
        return id;
    }

    private Task<PurchaseRequest> RequestAsync(int id) =>
        factory.RunAsync((_, db) => db.PurchaseRequests.AsNoTracking().SingleAsync(r => r.Id == id, Ct));

    private Task<List<RequestAction>> EventsAsync(int id) =>
        factory.RunAsync((_, db) => db.RequestEvents.Where(e => e.PurchaseRequestId == id).OrderBy(e => e.Id).Select(e => e.Action).ToListAsync(Ct));

    private Task<List<StockMovement>> ReceiptMovementsAsync() =>
        factory.RunAsync((_, db) => db.StockMovements.Where(m => m.Action == StockAction.Receipt).ToListAsync(Ct));
}
