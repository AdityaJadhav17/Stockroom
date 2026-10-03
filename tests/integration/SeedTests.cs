using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockroom.Web.Data;
using Stockroom.Web.Models;

namespace Stockroom.IntegrationTests;

// T-14 (partial): seed contents, role assignment, and rerun behavior against SQLite (US-01, US-08).
public sealed class SeedTests : IAsyncLifetime
{
    private readonly StockroomFactory factory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.SeedAsync();

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Theory]
    [InlineData(DemoSeeder.Member1Email, Roles.Member)]
    [InlineData(DemoSeeder.Member2Email, Roles.Member)]
    [InlineData(DemoSeeder.ManagerEmail, Roles.Manager)]
    public async Task SeedAssignsExactlyOneRole(string email, string role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = await users.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.Equal([role], await users.GetRolesAsync(user));
    }

    [Fact]
    public async Task SeedCreatesTenItemsWithOpeningMovements()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var items = await db.InventoryItems.ToListAsync(Ct);
        var openings = await db.StockMovements.Where(m => m.Action == StockAction.Opening).ToListAsync(Ct);

        Assert.Equal(10, items.Count);
        Assert.Equal(3, await db.Users.CountAsync(Ct));
        var filament = Assert.Single(items, i => i.Sku == "FIL-PLA-175");
        Assert.Equal(2, filament.Quantity);
        Assert.Equal(3, filament.ReorderThreshold);
        Assert.All(items, item => Assert.Equal(item.Quantity, openings.Where(m => m.ItemId == item.Id).Sum(m => m.QuantityDelta)));
    }

    [Fact]
    public async Task SeedRerunPreservesExistingRecords()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Simulate a later stock change that a rerun must not overwrite.
            var filament = await db.InventoryItems.SingleAsync(i => i.Sku == "FIL-PLA-175", Ct);
            filament.Quantity = 9;
            await db.SaveChangesAsync(Ct);
        }
        var before = await SnapshotAsync();

        await factory.SeedAsync();

        Assert.Equal(before, await SnapshotAsync());
        Assert.Contains("FIL-PLA-175=9", before);
    }

    private async Task<string> SnapshotAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await db.Users.OrderBy(u => u.Email).Select(u => u.Id + u.Email + u.PasswordHash).ToListAsync(Ct);
        var userRoles = await db.UserRoles.OrderBy(r => r.UserId).ThenBy(r => r.RoleId).Select(r => r.UserId + r.RoleId).ToListAsync(Ct);
        var items = await db.InventoryItems.OrderBy(i => i.Sku).Select(i => i.Sku + "=" + i.Quantity).ToListAsync(Ct);
        var movements = await db.StockMovements.OrderBy(m => m.Id).Select(m => m.Id + ":" + m.ItemId + ":" + m.QuantityDelta).ToListAsync(Ct);
        return string.Join("|", users.Concat(userRoles).Concat(items).Concat(movements));
    }
}
