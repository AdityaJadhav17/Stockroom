using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Models;

namespace Stockroom.Web.Data;

// Creates the synthetic demo dataset (US-08). A rerun adds only missing roles, accounts, role memberships,
// and items; it never changes existing records. Example requests are written only in the run that creates
// the items, so an existing database never receives them. Reset is a separate, explicit option.
public static class DemoSeeder
{
    public const string Member1Email = "member1@stockroom.test";
    public const string Member2Email = "member2@stockroom.test";
    public const string ManagerEmail = "manager@stockroom.test";

    // Fixed UTC times keep the dataset identical on every fresh seed or reset.
    public static readonly DateTime OpeningUtc = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    // Opening quantities. Example receipts and issues change some of them; filament stays at two of three.
    private static readonly (string Sku, string Name, string Unit, int Quantity, int ReorderThreshold)[] Items =
    [
        ("FIL-PLA-175", "PLA filament, 1.75 mm", "spool", 2, 3),
        ("RSN-STD-1L", "Standard photopolymer resin, 1 L", "bottle", 4, 2),
        ("PLY-3MM-A4", "Birch plywood sheet, 3 mm", "pack", 6, 2),
        ("ACR-3MM-A4", "Clear acrylic sheet, 3 mm", "pack", 5, 2),
        ("SLD-WIRE-08", "Lead-free solder wire, 0.8 mm", "roll", 3, 1),
        ("JMP-WIRE-MM", "Jumper wires, male to male", "pack", 8, 3),
        ("RES-KIT-025W", "Resistor assortment, 0.25 W", "box", 2, 1),
        ("LED-5MM-MIX", "LED assortment, 5 mm", "box", 4, 2),
        ("MCU-NANO", "Microcontroller board, Nano format", "board", 10, 4),
        ("TAPE-PI-20", "Polyimide tape, 20 mm", "roll", 3, 1),
    ];

    private sealed record ExampleRequest(
        string Sku, int Quantity, string Reason, string RequesterEmail, RequestStatus Status,
        DateTime CreatedUtc, DateTime? ReviewedUtc = null, string? RejectionReason = null, DateTime? ReceivedUtc = null);

    // One request in each status. Filament is left out so the purchase demo starts from two spools.
    private static readonly ExampleRequest[] ExampleRequests =
    [
        new("PLY-3MM-A4", 4, "Panels for the laser cutter class", Member1Email, RequestStatus.Received,
            Day(2, 9), ReviewedUtc: Day(2, 14), ReceivedUtc: Day(4, 10)),
        new("MCU-NANO", 20, "Boards for a personal drone project", Member2Email, RequestStatus.Rejected,
            Day(3, 11), ReviewedUtc: Day(3, 15), RejectionReason: "Exceeds the per-member limit. Request a class kit instead."),
        new("RSN-STD-1L", 3, "Calibration prints for the resin printer", Member2Email, RequestStatus.Approved,
            Day(5, 10), ReviewedUtc: Day(5, 16)),
        new("LED-5MM-MIX", 2, "Wearables workshop", Member1Email, RequestStatus.Pending, Day(6, 9)),
    ];

    private static readonly (string Sku, int Quantity, string Reason, DateTime OccurredUtc) ExampleIssue =
        ("JMP-WIRE-MM", 2, "Intro to electronics class kits", Day(5, 13));

    private static DateTime Day(int day, int hour) => new(2026, 9, day, hour, 0, 0, DateTimeKind.Utc);

    // Entry point for `dotnet run -- seed [--reset]`. Returns the process exit code.
    public static async Task<int> RunCommandAsync(
        IServiceProvider services, IHostEnvironment environment, bool reset, TextWriter output, TextWriter error)
    {
        if (!environment.IsDevelopment())
        {
            error.WriteLine($"The seed command runs only in Development. Current environment: {environment.EnvironmentName}. Nothing was changed.");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        // Check the passwords first, so a missing or rejected password cannot fail after a reset deletes data.
        if (await SeedSettingsErrorAsync(scope.ServiceProvider) is { } settingsError)
        {
            error.WriteLine($"Seed refused: {settingsError} Nothing was changed.");
            return 1;
        }

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (reset)
        {
            var problem = ResetTargetError(db.Database.GetConnectionString(), out var path);
            if (problem is not null)
            {
                error.WriteLine($"Reset refused: {problem} Nothing was changed.");
                return 1;
            }
            output.WriteLine($"Reset: deleting the local demo database {path} and recreating it.");
            // Close pooled connections to this database only, then delete the validated file. EF Core's
            // EnsureDeletedAsync is avoided because it clears every SQLite pool in the process.
            SqliteConnection.ClearPool((SqliteConnection)db.Database.GetDbConnection());
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // On Windows a running application keeps the file open, so deletion fails before any change.
                error.WriteLine($"Reset failed: {ex.Message} Stop the application and try again. Nothing was changed.");
                return 1;
            }
        }

        await SeedAsync(scope.ServiceProvider);
        output.WriteLine(
            $"Seed complete: {await db.Users.CountAsync()} accounts, {await db.InventoryItems.CountAsync()} items, " +
            $"{await db.PurchaseRequests.CountAsync()} purchase requests, {await db.StockMovements.CountAsync()} stock movements.");
        return 0;
    }

    // Reset deletes one file, so it accepts only a plain local path to a .db file that is absent or already a
    // SQLite database. Memory databases, URIs, wildcards, directories, and other files are refused.
    public static string? ResetTargetError(string? connectionString, out string path)
    {
        path = "";
        SqliteConnectionStringBuilder builder;
        try
        {
            builder = new SqliteConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            return "The connection string is not valid.";
        }

        var source = builder.DataSource;
        if (string.IsNullOrWhiteSpace(source) || source == ":memory:" || builder.Mode != SqliteOpenMode.ReadWriteCreate
            || source.StartsWith("file:", StringComparison.OrdinalIgnoreCase) || source.IndexOfAny(['*', '?', '|']) >= 0)
        {
            return $"The data source \"{source}\" is not a plain local SQLite file path.";
        }

        path = Path.GetFullPath(source);
        if (!path.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            return $"Reset deletes only .db files, and {path} has another extension.";
        }
        if (Directory.Exists(path))
        {
            return $"{path} is a directory.";
        }
        if (File.Exists(path))
        {
            try
            {
                if (!IsSqliteFile(path))
                {
                    return $"{path} is not a SQLite database.";
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return $"{path} could not be read ({ex.Message}). Stop any process using it and try again.";
            }
        }
        return null;
    }

    // Applies Identity's password rules to both seed passwords without creating any account.
    private static async Task<string?> SeedSettingsErrorAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        foreach (var (key, email) in new[] { ("Seed:MemberPassword", Member1Email), ("Seed:ManagerPassword", ManagerEmail) })
        {
            var password = configuration[key];
            if (string.IsNullOrEmpty(password))
            {
                return $"Set {key} with dotnet user-secrets or the {key.Replace(":", "__")} environment variable.";
            }
            foreach (var validator in userManager.PasswordValidators)
            {
                var result = await validator.ValidateAsync(userManager, new IdentityUser { UserName = email, Email = email }, password);
                if (!result.Succeeded)
                {
                    return $"{key} does not meet the password rules: {string.Join(" ", result.Errors.Select(e => e.Description))}";
                }
            }
        }
        return null;
    }

    private static bool IsSqliteFile(string path)
    {
        var header = "SQLite format 3\0"u8;
        // Share access so the check works while a pooled SQLite connection still holds the file open.
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (file.Length == 0)
        {
            return true;
        }
        Span<byte> buffer = stackalloc byte[16];
        return file.Read(buffer) == 16 && buffer.SequenceEqual(header);
    }

    public static async Task SeedAsync(IServiceProvider services, bool includeExamples = true)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var memberPassword = RequiredSetting(configuration, "Seed:MemberPassword");
        var managerPassword = RequiredSetting(configuration, "Seed:ManagerPassword");

        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Member, Roles.Manager })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                Check(await roleManager.CreateAsync(new IdentityRole(role)), $"create role {role}");
            }
        }

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var users = new Dictionary<string, string>
        {
            [Member1Email] = (await EnsureUserAsync(userManager, Member1Email, memberPassword, Roles.Member)).Id,
            [Member2Email] = (await EnsureUserAsync(userManager, Member2Email, memberPassword, Roles.Member)).Id,
            [ManagerEmail] = (await EnsureUserAsync(userManager, ManagerEmail, managerPassword, Roles.Manager)).Id,
        };
        var managerId = users[ManagerEmail];

        var existingSkus = await db.InventoryItems.Select(i => i.Sku).ToListAsync();
        var freshDatabase = existingSkus.Count == 0;

        // Items, opening movements, and examples commit together or not at all.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var items = new Dictionary<string, InventoryItem>();
        foreach (var seed in Items.Where(i => !existingSkus.Contains(i.Sku)))
        {
            var item = new InventoryItem
            {
                Sku = seed.Sku,
                Name = seed.Name,
                Unit = seed.Unit,
                Quantity = seed.Quantity,
                ReorderThreshold = seed.ReorderThreshold,
            };
            items[seed.Sku] = item;
            db.InventoryItems.Add(item);
            db.StockMovements.Add(new StockMovement
            {
                Item = item,
                QuantityDelta = seed.Quantity,
                Action = StockAction.Opening,
                ActorId = managerId,
                OccurredAtUtc = OpeningUtc,
                Reason = "Opening stock",
            });
        }
        await db.SaveChangesAsync();

        if (freshDatabase && includeExamples)
        {
            await AddExamplesAsync(db, items, users, managerId);
        }
        await transaction.CommitAsync();
    }

    // Writes the same records the services would: request fields, request events, receipt and issue
    // movements, and the matching item quantities.
    private static async Task AddExamplesAsync(
        AppDbContext db, Dictionary<string, InventoryItem> items, Dictionary<string, string> users, string managerId)
    {
        foreach (var example in ExampleRequests)
        {
            var item = items[example.Sku];
            var request = new PurchaseRequest
            {
                ItemId = item.Id,
                Quantity = example.Quantity,
                Reason = example.Reason,
                RequesterId = users[example.RequesterEmail],
                Status = example.Status,
                CreatedAtUtc = example.CreatedUtc,
                ReviewerId = example.ReviewedUtc is null ? null : managerId,
                ReviewedAtUtc = example.ReviewedUtc,
                RejectionReason = example.RejectionReason,
                ReceiverId = example.ReceivedUtc is null ? null : managerId,
                ReceivedAtUtc = example.ReceivedUtc,
            };
            db.PurchaseRequests.Add(request);
            await db.SaveChangesAsync();

            AddEvent(db, request.Id, RequestAction.Created, request.RequesterId, example.CreatedUtc, null);
            if (example.ReviewedUtc is { } reviewed)
            {
                var decision = example.Status == RequestStatus.Rejected ? RequestAction.Rejected : RequestAction.Approved;
                AddEvent(db, request.Id, decision, managerId, reviewed, example.RejectionReason);
            }
            if (example.ReceivedUtc is { } received)
            {
                AddEvent(db, request.Id, RequestAction.Received, managerId, received, null);
                item.Quantity += example.Quantity;
                db.StockMovements.Add(new StockMovement
                {
                    ItemId = item.Id,
                    QuantityDelta = example.Quantity,
                    Action = StockAction.Receipt,
                    ActorId = managerId,
                    OccurredAtUtc = received,
                    PurchaseRequestId = request.Id,
                });
            }
        }

        var issued = items[ExampleIssue.Sku];
        issued.Quantity -= ExampleIssue.Quantity;
        db.StockMovements.Add(new StockMovement
        {
            ItemId = issued.Id,
            QuantityDelta = -ExampleIssue.Quantity,
            Action = StockAction.Issue,
            ActorId = managerId,
            OccurredAtUtc = ExampleIssue.OccurredUtc,
            Reason = ExampleIssue.Reason,
        });
        await db.SaveChangesAsync();
    }

    private static void AddEvent(AppDbContext db, int requestId, RequestAction action, string actorId, DateTime at, string? note) =>
        db.RequestEvents.Add(new RequestEvent
        {
            PurchaseRequestId = requestId,
            Action = action,
            ActorId = actorId,
            OccurredAtUtc = at,
            Note = note,
        });

    private static async Task<IdentityUser> EnsureUserAsync(
        UserManager<IdentityUser> userManager, string email, string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            Check(await userManager.CreateAsync(user, password), $"create {email}");
        }
        if (!await userManager.IsInRoleAsync(user, role))
        {
            Check(await userManager.AddToRoleAsync(user, role), $"add {email} to {role}");
        }
        return user;
    }

    private static string RequiredSetting(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Set {key} with dotnet user-secrets or the {key.Replace(":", "__")} environment variable.");

    private static void Check(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }
}
