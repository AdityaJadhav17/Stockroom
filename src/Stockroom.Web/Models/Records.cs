namespace Stockroom.Web.Models;

public static class Roles
{
    public const string Member = "Member";
    public const string Manager = "Manager";
}

public enum RequestStatus { Pending, Approved, Rejected, Received }

public enum RequestAction { Created, Approved, Rejected, Received }

public enum StockAction { Opening, Receipt, Issue }

public class InventoryItem
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Unit { get; set; }
    public int Quantity { get; set; }
    public int ReorderThreshold { get; set; }
}

public class PurchaseRequest
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public InventoryItem? Item { get; set; }
    public int Quantity { get; set; }
    public required string Reason { get; set; }
    public required string RequesterId { get; set; }
    public RequestStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? ReviewerId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public string? ReceiverId { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }
}

public class StockMovement
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public InventoryItem? Item { get; set; }
    public int QuantityDelta { get; set; }
    public StockAction Action { get; set; }
    public required string ActorId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Reason { get; set; }
    public int? PurchaseRequestId { get; set; }
}

public class RequestEvent
{
    public int Id { get; set; }
    public int PurchaseRequestId { get; set; }
    public RequestAction Action { get; set; }
    public required string ActorId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Note { get; set; }
}
