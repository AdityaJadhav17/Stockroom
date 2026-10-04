using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Models;

namespace Stockroom.Web.Services;

public sealed record OperationResult(bool Succeeded, string Message, int? RequestId = null);

public sealed record RequestView(
    int Id, int ItemId, string ItemName, string Unit, int Quantity, string Reason, RequestStatus Status,
    string RequesterId, string RequesterEmail, DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc, string? RejectionReason, DateTime? ReceivedAtUtc);

public sealed record RequestEventView(
    int Id, int RequestId, RequestAction Action, string ActorEmail, DateTime OccurredAtUtc,
    string ItemName, string Unit, int Quantity, string? Note);

// Input and role rules shared by the pages and the service (BR-02, BR-03, BR-10).
public static class PurchaseRules
{
    public const int MaxQuantity = 10_000;
    public const int MaxReasonLength = 500;
    public const string RequesterPolicy = "PurchaseRequester";

    public static string? QuantityError(int? quantity) =>
        quantity is null or < 1 or > MaxQuantity ? $"Enter a whole number from 1 to {MaxQuantity:N0}." : null;

    public static string? ReasonError(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? "Enter a reason."
        : reason.Trim().Length > MaxReasonLength ? $"Use {MaxReasonLength} characters or fewer."
        : null;

    // BR-02: a Manager cannot create purchases, even when the account also holds the Member role.
    public static bool CanRequestPurchases(ClaimsPrincipal user) =>
        user.IsInRole(Roles.Member) && !user.IsInRole(Roles.Manager);
}

// Purchase request rules (BR-01 through BR-08, BR-11). Each operation checks the actor's roles in the
// database, then claims the row with a conditional UPDATE inside a transaction. ServiceGuard turns
// exhausted SQLite lock retries into BusyMessage after the transaction rolls back.
public sealed class PurchaseService(AppDbContext db, ILogger<PurchaseService> logger)
{
    public const string ForbiddenMessage = ServiceGuard.ForbiddenMessage;
    public const string BusyMessage = ServiceGuard.BusyMessage;

    // Ownership rule: managers see every request; members see only their own. The Manager role comes from
    // the database inside the query, so a caller cannot widen visibility. Filters run before the
    // projection because EF cannot translate predicates on a positional record.
    public IQueryable<RequestView> VisibleRequests(string viewerId, int? requestId = null, RequestStatus? status = null)
    {
        var viewerIsManager = db.RoleMemberships(viewerId, Roles.Manager);
        return
            from r in db.PurchaseRequests
            join i in db.InventoryItems on r.ItemId equals i.Id
            join u in db.Users on r.RequesterId equals u.Id
            where (r.RequesterId == viewerId || viewerIsManager.Any())
                && (requestId == null || r.Id == requestId)
                && (status == null || r.Status == status)
            orderby r.Id descending
            select new RequestView(r.Id, i.Id, i.Name, i.Unit, r.Quantity, r.Reason, r.Status, r.RequesterId, u.Email!,
                r.CreatedAtUtc, r.ReviewedAtUtc, r.RejectionReason, r.ReceivedAtUtc);
    }

    public Task<OperationResult> CreateAsync(string requesterId, int itemId, int? quantity, string? reason) =>
        db.GuardAsync(logger, async () =>
        {
            if (!await db.HasRoleAsync(requesterId, Roles.Member) || await db.HasRoleAsync(requesterId, Roles.Manager))
            {
                return new(false, ForbiddenMessage);
            }
            var error = PurchaseRules.QuantityError(quantity) ?? PurchaseRules.ReasonError(reason);
            if (error is not null)
            {
                return new(false, error);
            }
            if (!await db.InventoryItems.AnyAsync(i => i.Id == itemId))
            {
                return new(false, "Select an item.");
            }

            var now = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync();
            var request = new PurchaseRequest
            {
                ItemId = itemId,
                Quantity = quantity!.Value,
                Reason = reason!.Trim(),
                RequesterId = requesterId,
                Status = RequestStatus.Pending,
                CreatedAtUtc = now,
            };
            db.PurchaseRequests.Add(request);
            await db.SaveChangesAsync();
            db.RequestEvents.Add(Event(request.Id, RequestAction.Created, requesterId, now, null));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new(true, $"Request #{request.Id} submitted.", request.Id);
        });

    public Task<OperationResult> ApproveAsync(int requestId, string reviewerId) =>
        ReviewAsync(requestId, reviewerId, RequestStatus.Approved, null);

    public Task<OperationResult> RejectAsync(int requestId, string reviewerId, string? reason) =>
        ReviewAsync(requestId, reviewerId, RequestStatus.Rejected, reason);

    private Task<OperationResult> ReviewAsync(int requestId, string reviewerId, RequestStatus decision, string? reason) =>
        db.GuardAsync(logger, async () =>
        {
            if (!await db.HasRoleAsync(reviewerId, Roles.Manager))
            {
                return new(false, ForbiddenMessage, requestId);
            }
            if (decision == RequestStatus.Rejected && PurchaseRules.ReasonError(reason) is { } error)
            {
                return new(false, $"Rejection reason: {error}", requestId);
            }

            var note = reason?.Trim();
            var now = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync();
            var claimed = await db.PurchaseRequests
                .Where(r => r.Id == requestId && r.Status == RequestStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, decision)
                    .SetProperty(r => r.ReviewerId, reviewerId)
                    .SetProperty(r => r.ReviewedAtUtc, now)
                    .SetProperty(r => r.RejectionReason, note));
            if (claimed == 0)
            {
                return await CurrentStateAsync(requestId);
            }

            var action = decision == RequestStatus.Approved ? RequestAction.Approved : RequestAction.Rejected;
            db.RequestEvents.Add(Event(requestId, action, reviewerId, now, note));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new(true, $"Request #{requestId} {decision.ToString().ToLowerInvariant()}.", requestId);
        });

    public Task<OperationResult> ReceiveAsync(int requestId, string receiverId) =>
        db.GuardAsync(logger, async () =>
        {
            if (!await db.HasRoleAsync(receiverId, Roles.Manager))
            {
                return new(false, ForbiddenMessage, requestId);
            }
            var request = await db.PurchaseRequests.AsNoTracking()
                .Where(r => r.Id == requestId)
                .Select(r => new { r.ItemId, r.Quantity, r.Item!.Unit })
                .SingleOrDefaultAsync();
            if (request is null)
            {
                return new(false, $"Request #{requestId} does not exist.");
            }

            var now = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync();
            var claimed = await db.PurchaseRequests
                .Where(r => r.Id == requestId && r.Status == RequestStatus.Approved)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, RequestStatus.Received)
                    .SetProperty(r => r.ReceiverId, receiverId)
                    .SetProperty(r => r.ReceivedAtUtc, now));
            if (claimed == 0)
            {
                return await CurrentStateAsync(requestId);
            }

            // The condition keeps the stored quantity within the Int32 range; returning without commit rolls back the claim.
            var stocked = await db.InventoryItems
                .Where(i => i.Id == request.ItemId && i.Quantity <= int.MaxValue - request.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, i => i.Quantity + request.Quantity));
            if (stocked == 0)
            {
                return new(false, $"Receiving request #{requestId} would exceed the maximum stock quantity. Nothing was changed.", requestId);
            }

            db.StockMovements.Add(new StockMovement
            {
                ItemId = request.ItemId,
                QuantityDelta = request.Quantity,
                Action = StockAction.Receipt,
                ActorId = receiverId,
                OccurredAtUtc = now,
                PurchaseRequestId = requestId,
            });
            db.RequestEvents.Add(Event(requestId, RequestAction.Received, receiverId, now, null));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new(true, $"Request #{requestId} received. Added {DisplayFormat.Quantity(request.Quantity, request.Unit)} to stock.", requestId);
        });

    // US-07: managers see every request event; members see events on their own requests only, with the
    // Manager role read from the database. A Created event shows the purchase reason as its note.
    // Newest first, with the identifier breaking ties between events saved in the same operation.
    public IQueryable<RequestEventView> RequestHistory(string viewerId, int? requestId = null)
    {
        var viewerIsManager = db.RoleMemberships(viewerId, Roles.Manager);
        return
            from e in db.RequestEvents
            join r in db.PurchaseRequests on e.PurchaseRequestId equals r.Id
            join i in db.InventoryItems on r.ItemId equals i.Id
            join u in db.Users on e.ActorId equals u.Id
            where (r.RequesterId == viewerId || viewerIsManager.Any()) && (requestId == null || r.Id == requestId)
            orderby e.OccurredAtUtc descending, e.Id descending
            select new RequestEventView(e.Id, r.Id, e.Action, u.Email!, e.OccurredAtUtc, i.Name, i.Unit, r.Quantity,
                e.Action == RequestAction.Created ? r.Reason : e.Note);
    }

    private async Task<OperationResult> CurrentStateAsync(int requestId)
    {
        var status = await db.PurchaseRequests.Where(r => r.Id == requestId).Select(r => (RequestStatus?)r.Status).SingleOrDefaultAsync();
        return status is null
            ? new(false, $"Request #{requestId} does not exist.")
            : new(false, $"Request #{requestId} is {status}. Nothing was changed.", requestId);
    }

    private static RequestEvent Event(int requestId, RequestAction action, string actorId, DateTime now, string? note) =>
        new() { PurchaseRequestId = requestId, Action = action, ActorId = actorId, OccurredAtUtc = now, Note = note };
}

public static class DisplayFormat
{
    public static string Utc(DateTime? value) =>
        value?.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture) ?? "";

    // A <time> element with a machine-readable value; the stylesheet keeps it on one line in tables.
    // Both strings come from formatted dates, so they need no HTML encoding.
    public static Microsoft.AspNetCore.Html.HtmlString UtcTime(DateTime? value) =>
        value is { } time
            ? new($"<time datetime=\"{time.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)}\">{Utc(time)}</time>")
            : Microsoft.AspNetCore.Html.HtmlString.Empty;

    // "1 spool", "5 spools", "2 boxes". Units are single English nouns from the seeded items.
    public static string Quantity(int quantity, string unit) =>
        $"{quantity} {(Math.Abs(quantity) == 1 ? unit : Plural(unit))}";

    // A request status or action as a labelled badge. The text names the state, so colour is never the only cue.
    // Enum names need no HTML encoding.
    public static Microsoft.AspNetCore.Html.HtmlString Badge(Enum value) =>
        new($"<span class=\"badge badge-{value.ToString().ToLowerInvariant()}\">{value}</span>");

    // Stock changes with an explicit sign: "+5 spools", "-2 spools".
    public static string Change(int delta, string unit) => (delta > 0 ? "+" : "") + Quantity(delta, unit);

    private static string Plural(string unit) =>
        unit.EndsWith('s') || unit.EndsWith('x') || unit.EndsWith('z') || unit.EndsWith("ch") || unit.EndsWith("sh")
            ? unit + "es"
            : unit + "s";
}
