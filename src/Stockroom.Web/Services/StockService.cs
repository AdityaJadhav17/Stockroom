using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Models;

namespace Stockroom.Web.Services;

public sealed record MovementView(
    int Id, string ItemName, string Unit, int QuantityDelta, StockAction Action, string ActorEmail,
    DateTime OccurredAtUtc, string? Reason, int? PurchaseRequestId);

// Stock issues and movement history (BR-01, BR-03, BR-06, BR-07, BR-08, BR-10).
public sealed class StockService(AppDbContext db)
{
    public Task<OperationResult> IssueAsync(int itemId, string actorId, int? quantity, string? reason) =>
        db.GuardAsync(async () =>
        {
            if (!await db.HasRoleAsync(actorId, Roles.Manager))
            {
                return new(false, ServiceGuard.ForbiddenMessage);
            }
            var error = PurchaseRules.QuantityError(quantity) ?? PurchaseRules.ReasonError(reason);
            if (error is not null)
            {
                return new(false, error);
            }

            var issued = quantity!.Value;
            var now = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync();
            // BR-06: the WHERE clause, not an earlier read, decides whether enough stock remains.
            var reduced = await db.InventoryItems
                .Where(i => i.Id == itemId && i.Quantity >= issued)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, i => i.Quantity - issued));
            if (reduced == 0)
            {
                var item = await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == itemId);
                return item is null
                    ? new(false, "Select an item.")
                    : new(false, $"Only {item.Quantity} {item.Unit} of {item.Name} in stock. Nothing was changed.");
            }

            db.StockMovements.Add(new StockMovement
            {
                ItemId = itemId,
                QuantityDelta = -issued,
                Action = StockAction.Issue,
                ActorId = actorId,
                OccurredAtUtc = now,
                Reason = reason!.Trim(),
            });
            // Read inside the transaction so a lock after commit cannot turn a committed issue into an error.
            var issuedItem = await db.InventoryItems.AsNoTracking().SingleAsync(i => i.Id == itemId);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new(true, $"Issued {issued} {issuedItem.Unit} of {issuedItem.Name}. {issuedItem.Quantity} remain.");
        });

    // US-07: full movement history for managers only. The role comes from the database inside the query,
    // so any other caller receives no rows. Newest first; the identifier breaks ties.
    public IQueryable<MovementView> MovementHistory(string viewerId) =>
        from m in db.StockMovements
        join i in db.InventoryItems on m.ItemId equals i.Id
        join u in db.Users on m.ActorId equals u.Id
        where db.RoleMemberships(viewerId, Roles.Manager).Any()
        orderby m.OccurredAtUtc descending, m.Id descending
        select new MovementView(m.Id, i.Name, i.Unit, m.QuantityDelta, m.Action, u.Email!, m.OccurredAtUtc, m.Reason, m.PurchaseRequestId);
}
