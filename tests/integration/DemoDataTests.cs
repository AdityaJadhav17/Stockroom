using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stockroom.Web.Data;
using Stockroom.Web.Models;

namespace Stockroom.IntegrationTests;

// US-08 demo dataset, seed reruns (T-14), and reset (T-15) against disposable SQLite files.
public sealed class DemoDataTests : IAsyncLifetime
{
    private readonly StockroomFactory factory = new(seedExamples: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.SeedAsync();

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task FreshSeedCreatesDocumentedDataset()
    {
        var (items, requests, movements) = await factory.RunAsync(async (_, db) => (
            await db.InventoryItems.AsNoTracking().ToDictionaryAsync(i => i.Sku, Ct),
            await db.PurchaseRequests.AsNoTracking().ToListAsync(Ct),
            await db.StockMovements.AsNoTracking().ToListAsync(Ct)));

        Assert.Equal(3, await factory.RunAsync((_, db) => db.Users.CountAsync(Ct)));
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["FIL-PLA-175"] = 2, ["RSN-STD-1L"] = 4, ["PLY-3MM-A4"] = 10, ["ACR-3MM-A4"] = 5, ["SLD-WIRE-08"] = 3,
                ["JMP-WIRE-MM"] = 6, ["RES-KIT-025W"] = 2, ["LED-5MM-MIX"] = 4, ["MCU-NANO"] = 10, ["TAPE-PI-20"] = 3,
            },
            items.ToDictionary(p => p.Key, p => p.Value.Quantity));
        Assert.Equal(3, items["FIL-PLA-175"].ReorderThreshold);
        Assert.Equal(
            [RequestStatus.Pending, RequestStatus.Approved, RequestStatus.Rejected, RequestStatus.Received],
            requests.Select(r => r.Status).Order().ToList());
        Assert.DoesNotContain(requests, r => r.ItemId == items["FIL-PLA-175"].Id);
        Assert.Equal((10, 1, 1), (
            movements.Count(m => m.Action == StockAction.Opening),
            movements.Count(m => m.Action == StockAction.Receipt),
            movements.Count(m => m.Action == StockAction.Issue)));
    }

    [Fact]
    public async Task StockReconcilesWithOpeningReceiptAndIssueMovements()
    {
        var (items, movements) = await factory.RunAsync(async (_, db) => (
            await db.InventoryItems.AsNoTracking().ToListAsync(Ct),
            await db.StockMovements.AsNoTracking().ToListAsync(Ct)));

        Assert.All(items, item => Assert.Equal(item.Quantity, movements.Where(m => m.ItemId == item.Id).Sum(m => m.QuantityDelta)));
        Assert.All(items, item => Assert.Single(movements, m => m.ItemId == item.Id && m.Action == StockAction.Opening));
        var issue = Assert.Single(movements, m => m.Action == StockAction.Issue);
        Assert.Equal((-2, "Intro to electronics class kits"), (issue.QuantityDelta, issue.Reason));
    }

    [Fact]
    public async Task RequestHistoriesMatchTheirStatus()
    {
        var managerId = await factory.UserIdAsync(DemoSeeder.ManagerEmail);
        var (requests, events, receipts) = await factory.RunAsync(async (_, db) => (
            await db.PurchaseRequests.AsNoTracking().ToListAsync(Ct),
            await db.RequestEvents.AsNoTracking().OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.Id).ToListAsync(Ct),
            await db.StockMovements.AsNoTracking().Where(m => m.Action == StockAction.Receipt).ToListAsync(Ct)));

        foreach (var request in requests)
        {
            var history = events.Where(e => e.PurchaseRequestId == request.Id).ToList();
            RequestAction[] expected = request.Status switch
            {
                RequestStatus.Pending => [RequestAction.Created],
                RequestStatus.Approved => [RequestAction.Created, RequestAction.Approved],
                RequestStatus.Rejected => [RequestAction.Created, RequestAction.Rejected],
                _ => [RequestAction.Created, RequestAction.Approved, RequestAction.Received],
            };
            Assert.Equal(expected, history.Select(e => e.Action));
            Assert.Equal((request.RequesterId, request.CreatedAtUtc), (history[0].ActorId, history[0].OccurredAtUtc));
            Assert.All(history.Skip(1), e => Assert.Equal(managerId, e.ActorId));
            Assert.Equal(request.Status == RequestStatus.Pending ? null : managerId, request.ReviewerId);
            Assert.Equal(request.Status == RequestStatus.Pending ? null : request.ReviewedAtUtc, history.ElementAtOrDefault(1)?.OccurredAtUtc);

            var receipt = receipts.SingleOrDefault(m => m.PurchaseRequestId == request.Id);
            if (request.Status == RequestStatus.Received)
            {
                Assert.NotNull(receipt);
                Assert.Equal((request.ItemId, request.Quantity, request.ReceivedAtUtc), (receipt.ItemId, receipt.QuantityDelta, (DateTime?)receipt.OccurredAtUtc));
                Assert.Equal(managerId, request.ReceiverId);
            }
            else
            {
                Assert.Null(receipt);
                Assert.Null(request.ReceiverId);
            }

            var rejected = history.SingleOrDefault(e => e.Action == RequestAction.Rejected);
            Assert.Equal(request.RejectionReason, rejected?.Note);
        }
    }

    [Fact]
    public async Task SeedRerunPreservesTheCompleteDatasetAndLaterChanges()
    {
        // Later activity through the services: a new request, a review, and an issue.
        var memberId = await factory.UserIdAsync(DemoSeeder.Member1Email);
        var managerId = await factory.UserIdAsync(DemoSeeder.ManagerEmail);
        var filament = await factory.ItemAsync("FIL-PLA-175");
        var created = await factory.RunAsync((s, _) => s.CreateAsync(memberId, filament.Id, 5, "Robotics workshop"));
        await factory.RunAsync((s, _) => s.ApproveAsync(created.RequestId!.Value, managerId));
        await factory.StockAsync(s => s.IssueAsync(filament.Id, managerId, 1, "Open house display"));
        var before = await DatasetAsync(factory, includeIdentifiers: true);

        await factory.SeedAsync();
        await factory.SeedAsync();

        Assert.Equal(before, await DatasetAsync(factory, includeIdentifiers: true));
        Assert.Equal(5, await factory.RunAsync((_, db) => db.PurchaseRequests.CountAsync(Ct)));
    }

    [Fact]
    public async Task SeedRerunOnExistingDatabaseNeverAddsExamples()
    {
        // A database seeded before example data existed keeps its records and quantities.
        await using var existing = new StockroomFactory();
        await existing.SeedAsync();
        var before = await DatasetAsync(existing, includeIdentifiers: true);

        await using (var scope = existing.Services.CreateAsyncScope())
        {
            await DemoSeeder.SeedAsync(scope.ServiceProvider, includeExamples: true);
        }

        Assert.Equal(before, await DatasetAsync(existing, includeIdentifiers: true));
        Assert.Equal(0, await existing.RunAsync((_, db) => db.PurchaseRequests.CountAsync(Ct)));
    }

    [Fact]
    public async Task ResetRestoresThePredictableDataset()
    {
        var memberId = await factory.UserIdAsync(DemoSeeder.Member1Email);
        var managerId = await factory.UserIdAsync(DemoSeeder.ManagerEmail);
        var filament = await factory.ItemAsync("FIL-PLA-175");
        await factory.RunAsync((s, _) => s.CreateAsync(memberId, filament.Id, 5, "Robotics workshop"));
        await factory.StockAsync(s => s.IssueAsync(filament.Id, managerId, 2, "Open house display"));
        await using var reference = new StockroomFactory(seedExamples: true);
        await reference.SeedAsync();
        var output = new StringWriter();

        var exitCode = await RunCommandAsync(factory, reset: true, output, new StringWriter());

        Assert.Equal(0, exitCode);
        Assert.Contains($"Reset: deleting the local demo database {Path.GetFullPath(factory.DatabasePath)}", output.ToString());
        Assert.Contains("Seed complete: 3 accounts, 10 items, 4 purchase requests, 12 stock movements.", output.ToString());
        Assert.Equal(await DatasetAsync(reference, includeIdentifiers: false), await DatasetAsync(factory, includeIdentifiers: false));
        Assert.Equal(2, (await factory.ItemAsync("FIL-PLA-175")).Quantity);
    }

    [Fact]
    public async Task ResetLeavesOtherDatabasePoolsOpen()
    {
        // A TEMP table lives only on one physical connection, so it survives in another database's pool
        // only if reset does not clear that pool.
        var otherPath = Path.Combine(Path.GetTempPath(), $"stockroom-other-{Guid.NewGuid():N}.db");
        var other = $"Data Source={otherPath}";
        try
        {
            await using (var connection = new SqliteConnection(other))
            {
                await connection.OpenAsync(Ct);
                await Execute(connection, "CREATE TEMP TABLE pool_marker (id INTEGER)");
            }
            Assert.True(await HasMarkerAsync(other), "The idle pooled connection should be reused.");

            var exitCode = await RunCommandAsync(factory, reset: true, new StringWriter(), new StringWriter());

            Assert.Equal(0, exitCode);
            Assert.True(await HasMarkerAsync(other), "Reset closed a pooled connection to another database.");
        }
        finally
        {
            SqliteConnection.ClearPool(new SqliteConnection(other));
            File.Delete(otherPath);
        }

        static async Task<bool> HasMarkerAsync(string connectionString)
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(Ct);
            var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM sqlite_temp_master WHERE name = 'pool_marker'";
            return Convert.ToInt32(await command.ExecuteScalarAsync(Ct)) == 1;
        }

        static async Task Execute(SqliteConnection connection, string sql)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(Ct);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SeedCommandIsRefusedOutsideDevelopment(bool reset)
    {
        await using var production = new StockroomFactory(seedExamples: true, environment: "Production");
        await production.SeedAsync();
        var before = await DatasetAsync(production, includeIdentifiers: true);
        var error = new StringWriter();

        var exitCode = await RunCommandAsync(production, reset, new StringWriter(), error);

        Assert.Equal(1, exitCode);
        Assert.Contains("runs only in Development. Current environment: Production. Nothing was changed.", error.ToString());
        Assert.Equal(before, await DatasetAsync(production, includeIdentifiers: true));
    }

    [Fact]
    public async Task ResetRefusesAConfiguredFileThatIsNotSqlite()
    {
        await using var unsupported = new StockroomFactory();
        await File.WriteAllTextAsync(unsupported.DatabasePath, "Workshop notes, not a database.", Ct);
        var error = new StringWriter();

        var exitCode = await RunCommandAsync(unsupported, reset: true, new StringWriter(), error);

        Assert.Equal(1, exitCode);
        Assert.Contains("is not a SQLite database. Nothing was changed.", error.ToString());
        Assert.Equal("Workshop notes, not a database.", await File.ReadAllTextAsync(unsupported.DatabasePath, Ct));
    }

    [Fact]
    public async Task ResetReportsAFileHeldOpenByAnotherProcess()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows prevents deleting an open file.");
        var before = await DatasetAsync(factory, includeIdentifiers: true);
        var error = new StringWriter();
        int exitCode;

        // Simulates the running application holding the database open.
        await using (new FileStream(factory.DatabasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            exitCode = await RunCommandAsync(factory, reset: true, new StringWriter(), error);
        }

        Assert.Equal(1, exitCode);
        Assert.Contains("Stop the application and try again. Nothing was changed.", error.ToString());
        Assert.Equal(before, await DatasetAsync(factory, includeIdentifiers: true));
    }

    // Case keys keep the rejected password values out of test names and CI reports (release security review R-01).
    [Theory]
    [InlineData("member-password-missing")]
    [InlineData("member-password-too-short")]
    [InlineData("manager-password-empty")]
    [InlineData("manager-password-without-uppercase")]
    public async Task InvalidSeedPasswordStopsResetBeforeDeletingAnything(string seedCase)
    {
        var (key, password, message) = seedCase switch
        {
            "member-password-missing" => ("Seed:MemberPassword", (string?)null, "Set Seed:MemberPassword"),
            "member-password-too-short" => ("Seed:MemberPassword", "weak", "Seed:MemberPassword does not meet the password rules"),
            "manager-password-empty" => ("Seed:ManagerPassword", "", "Set Seed:ManagerPassword"),
            _ => ("Seed:ManagerPassword", "alllowercase1!", "Seed:ManagerPassword does not meet the password rules"),
        };
        factory.Services.GetRequiredService<IConfiguration>()[key] = password;
        factory.ClearPool();
        var before = SHA256.HashData(await File.ReadAllBytesAsync(factory.DatabasePath, Ct));
        var error = new StringWriter();

        var exitCode = await RunCommandAsync(factory, reset: true, new StringWriter(), error);

        Assert.Equal(1, exitCode);
        Assert.Contains($"Seed refused: {message}", error.ToString());
        Assert.EndsWith("Nothing was changed.", error.ToString().TrimEnd());
        if (password is { Length: > 0 })
        {
            Assert.DoesNotContain(password, error.ToString(), StringComparison.Ordinal);
        }
        factory.ClearPool();
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(factory.DatabasePath, Ct)));
    }

    [Fact]
    public async Task InvalidSeedPasswordStopsSeedBeforeCreatingADatabase()
    {
        await using var fresh = new StockroomFactory(seedExamples: true);
        fresh.Services.GetRequiredService<IConfiguration>()["Seed:ManagerPassword"] = "weak";

        var exitCode = await RunCommandAsync(fresh, reset: false, new StringWriter(), new StringWriter());

        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(fresh.DatabasePath));
    }

    [Fact]
    public async Task ResetRefusesADatabaseLockedByAnotherProcess()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows enforces an exclusive file lock here.");
        factory.ClearPool();
        var before = SHA256.HashData(await File.ReadAllBytesAsync(factory.DatabasePath, Ct));
        var error = new StringWriter();
        int exitCode;

        // An exclusive lock, as another program might hold, blocks even the header check.
        await using (new FileStream(factory.DatabasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Contains("could not be read", DemoSeeder.ResetTargetError($"Data Source={factory.DatabasePath}", out _));
            exitCode = await RunCommandAsync(factory, reset: true, new StringWriter(), error);
        }

        Assert.Equal(1, exitCode);
        Assert.Contains("Stop any process using it and try again. Nothing was changed.", error.ToString());
        factory.ClearPool();
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(factory.DatabasePath, Ct)));
    }

    [Theory]
    [InlineData("Data Source=")]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=stockroom.db;Mode=Memory")]
    [InlineData("Data Source=stockroom.db;Mode=ReadOnly")]
    [InlineData("Data Source=file:stockroom.db?mode=memory")]
    [InlineData("Data Source=*.db")]
    [InlineData("Data Source=|DataDirectory|stockroom.db")]
    [InlineData("Data Source=stockroom.sqlite")]
    [InlineData("Data Source=stockroom")]
    [InlineData("Server=stockroom.db;Unsupported=yes")]
    public void ResetTargetRejectsUnsupportedConnectionStrings(string connectionString) =>
        Assert.NotNull(DemoSeeder.ResetTargetError(connectionString, out _));

    [Fact]
    public void ResetTargetRejectsDirectoriesAndAcceptsSqliteFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"stockroom-dir-{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(directory);
        try
        {
            Assert.Contains("is a directory", DemoSeeder.ResetTargetError($"Data Source={directory}", out _));
            Assert.True(Directory.Exists(directory));
        }
        finally
        {
            Directory.Delete(directory);
        }

        var missing = Path.Combine(Path.GetTempPath(), $"stockroom-missing-{Guid.NewGuid():N}.db");
        Assert.Null(DemoSeeder.ResetTargetError($"Data Source={missing}", out var resolved));
        Assert.Equal(missing, resolved);
        Assert.Null(DemoSeeder.ResetTargetError($"Data Source={factory.DatabasePath}", out _));
    }

    private static Task<int> RunCommandAsync(StockroomFactory target, bool reset, TextWriter output, TextWriter error) =>
        DemoSeeder.RunCommandAsync(target.Services, target.Services.GetRequiredService<IHostEnvironment>(), reset, output, error);

    // Every stored field of the dataset, keyed by email and SKU. includeIdentifiers adds database identifiers,
    // password hashes, and stamps, which a reset legitimately regenerates.
    private static Task<string> DatasetAsync(StockroomFactory target, bool includeIdentifiers) =>
        target.RunAsync(async (_, db) =>
        {
            static string T(DateTime? value) => value?.ToString("O", CultureInfo.InvariantCulture) ?? "-";
            var emails = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Email!, Ct);
            string E(string? id) => id is null ? "-" : emails[id];
            string Id(object id) => includeIdentifiers ? $"{id}:" : "";
            var roleNames = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name!, Ct);
            var skus = await db.InventoryItems.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.Sku, Ct);
            var requests = await db.PurchaseRequests.AsNoTracking().ToDictionaryAsync(r => r.Id, Ct);
            string R(int? id) => id is null ? "-" : $"{Id(id)}{skus[requests[id.Value].ItemId]}@{T(requests[id.Value].CreatedAtUtc)}";

            var lines = new List<string>();
            lines.AddRange((await db.Users.AsNoTracking().ToListAsync(Ct)).OrderBy(u => u.Email).Select(u =>
                $"user {Id(u.Id)}{u.Email} {u.UserName} {u.EmailConfirmed} {u.LockoutEnabled} {u.AccessFailedCount}" +
                (includeIdentifiers ? $" {u.PasswordHash} {u.SecurityStamp} {u.ConcurrencyStamp}" : "")));
            lines.AddRange((await db.UserRoles.AsNoTracking().ToListAsync(Ct)).Select(r => $"role {E(r.UserId)} {roleNames[r.RoleId]}").Order());
            lines.AddRange((await db.InventoryItems.AsNoTracking().ToListAsync(Ct)).OrderBy(i => i.Sku).Select(i =>
                $"item {Id(i.Id)}{i.Sku} {i.Name} {i.Unit} {i.Quantity} {i.ReorderThreshold}"));
            lines.AddRange(requests.Values.OrderBy(r => r.CreatedAtUtc).ThenBy(r => r.Id).Select(r =>
                $"request {R(r.Id)} {r.Quantity} {r.Reason} {E(r.RequesterId)} {r.Status} {E(r.ReviewerId)} {T(r.ReviewedAtUtc)} " +
                $"{r.RejectionReason ?? "-"} {E(r.ReceiverId)} {T(r.ReceivedAtUtc)}"));
            lines.AddRange((await db.RequestEvents.AsNoTracking().ToListAsync(Ct)).OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.Id).Select(e =>
                $"event {Id(e.Id)}{R(e.PurchaseRequestId)} {e.Action} {E(e.ActorId)} {T(e.OccurredAtUtc)} {e.Note ?? "-"}"));
            lines.AddRange((await db.StockMovements.AsNoTracking().ToListAsync(Ct)).OrderBy(m => m.OccurredAtUtc).ThenBy(m => m.Id).Select(m =>
                $"movement {Id(m.Id)}{skus[m.ItemId]} {m.QuantityDelta} {m.Action} {E(m.ActorId)} {T(m.OccurredAtUtc)} {m.Reason ?? "-"} {R(m.PurchaseRequestId)}"));
            return string.Join("\n", lines);
        });
}
