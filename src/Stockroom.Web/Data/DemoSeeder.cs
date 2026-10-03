using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Models;

namespace Stockroom.Web.Data;

// Creates synthetic demo data. A rerun leaves existing accounts, items, and movements unchanged.
public static class DemoSeeder
{
    public const string Member1Email = "member1@stockroom.test";
    public const string Member2Email = "member2@stockroom.test";
    public const string ManagerEmail = "manager@stockroom.test";

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

    public static async Task SeedAsync(IServiceProvider services)
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
        await EnsureUserAsync(userManager, Member1Email, memberPassword, Roles.Member);
        await EnsureUserAsync(userManager, Member2Email, memberPassword, Roles.Member);
        var manager = await EnsureUserAsync(userManager, ManagerEmail, managerPassword, Roles.Manager);

        var existingSkus = await db.InventoryItems.Select(i => i.Sku).ToListAsync();
        var now = DateTime.UtcNow;
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
            db.InventoryItems.Add(item);
            db.StockMovements.Add(new StockMovement
            {
                Item = item,
                QuantityDelta = seed.Quantity,
                Action = StockAction.Opening,
                ActorId = manager.Id,
                OccurredAtUtc = now,
                Reason = "Opening stock",
            });
        }

        // Items and their opening movements commit in one transaction.
        await db.SaveChangesAsync();
    }

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
