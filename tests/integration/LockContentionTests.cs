using Microsoft.Data.Sqlite;
using Stockroom.Web.Data;
using Stockroom.Web.Services;

namespace Stockroom.IntegrationTests;

// Architecture lock policy: when SQLite busy retries run out, the service returns a retry message and
// writes nothing. Another connection holds the write lock past a one-second timeout.
public sealed class LockContentionTests : IAsyncLifetime
{
    private readonly StockroomFactory factory = new(busyTimeoutSeconds: 1);
    private string memberId = "";
    private string managerId = "";
    private int filamentId;
    private int pendingId;
    private int approvedId;

    public async ValueTask InitializeAsync()
    {
        await factory.SeedAsync();
        memberId = await factory.UserIdAsync(DemoSeeder.Member1Email);
        managerId = await factory.UserIdAsync(DemoSeeder.ManagerEmail);
        filamentId = (await factory.ItemAsync("FIL-PLA-175")).Id;
        pendingId = await CreateAsync();
        approvedId = await CreateAsync();
        await factory.RunAsync((s, _) => s.ApproveAsync(approvedId, managerId));
    }

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Theory]
    [InlineData("Create")]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Receive")]
    public async Task ExhaustedBusyRetriesReturnRetryMessageWithoutWrites(string operation)
    {
        var target = operation == "Receive" ? approvedId : pendingId;
        var before = await factory.SnapshotAsync(target);
        var requestsBefore = await factory.RunAsync((_, db) => Task.FromResult(db.PurchaseRequests.Count()));

        OperationResult busy;
        await using (var holder = new SqliteConnection($"Data Source={factory.DatabasePath}"))
        {
            await holder.OpenAsync(TestContext.Current.CancellationToken);
            await using var hold = holder.BeginTransaction(deferred: false);
            busy = await RunAsync(operation, target);
            hold.Rollback();
        }

        Assert.False(busy.Succeeded);
        Assert.Equal(PurchaseService.BusyMessage, busy.Message);
        Assert.Equal(before, await factory.SnapshotAsync(target));
        Assert.Equal(requestsBefore, await factory.RunAsync((_, db) => Task.FromResult(db.PurchaseRequests.Count())));

        // The same operation succeeds once the lock is released.
        Assert.True((await RunAsync(operation, target)).Succeeded);
    }

    private Task<OperationResult> RunAsync(string operation, int requestId) =>
        factory.RunAsync((s, _) => operation switch
        {
            "Create" => s.CreateAsync(memberId, filamentId, 3, "Workshop supplies"),
            "Approve" => s.ApproveAsync(requestId, managerId),
            "Reject" => s.RejectAsync(requestId, managerId, "Not needed"),
            _ => s.ReceiveAsync(requestId, managerId),
        });

    private async Task<int> CreateAsync() =>
        (await factory.RunAsync((s, _) => s.CreateAsync(memberId, filamentId, 5, "Materials for the robotics workshop"))).RequestId!.Value;
}
