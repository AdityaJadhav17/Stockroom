using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Models;

namespace Stockroom.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext(options)
{
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<RequestEvent> RequestEvents => Set<RequestEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<InventoryItem>(item =>
        {
            item.HasIndex(i => i.Sku).IsUnique();
            item.Property(i => i.Sku).HasMaxLength(32);
            item.Property(i => i.Name).HasMaxLength(200);
            item.Property(i => i.Unit).HasMaxLength(32);
            item.ToTable(t =>
            {
                t.HasCheckConstraint("CK_InventoryItems_Quantity", "\"Quantity\" >= 0");
                t.HasCheckConstraint("CK_InventoryItems_ReorderThreshold", "\"ReorderThreshold\" >= 0");
            });
        });

        builder.Entity<PurchaseRequest>(request =>
        {
            request.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            request.Property(r => r.Reason).HasMaxLength(500);
            request.Property(r => r.RejectionReason).HasMaxLength(500);
            request.HasOne(r => r.Item).WithMany().HasForeignKey(r => r.ItemId).OnDelete(DeleteBehavior.Restrict);
            request.HasOne<IdentityUser>().WithMany().HasForeignKey(r => r.RequesterId).OnDelete(DeleteBehavior.Restrict);
            request.HasOne<IdentityUser>().WithMany().HasForeignKey(r => r.ReviewerId).OnDelete(DeleteBehavior.Restrict);
            request.HasOne<IdentityUser>().WithMany().HasForeignKey(r => r.ReceiverId).OnDelete(DeleteBehavior.Restrict);
            request.HasIndex(r => r.Status);
            request.ToTable(t =>
            {
                t.HasCheckConstraint("CK_PurchaseRequests_Quantity", "\"Quantity\" > 0");
                t.HasCheckConstraint("CK_PurchaseRequests_Reason", "length(trim(\"Reason\")) BETWEEN 1 AND 500");
                t.HasCheckConstraint("CK_PurchaseRequests_Status", "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Received')");
                t.HasCheckConstraint("CK_PurchaseRequests_RejectionReason",
                    "\"RejectionReason\" IS NULL OR length(trim(\"RejectionReason\")) BETWEEN 1 AND 500");
            });
        });

        builder.Entity<StockMovement>(movement =>
        {
            movement.Property(m => m.Action).HasConversion<string>().HasMaxLength(16);
            movement.Property(m => m.Reason).HasMaxLength(500);
            movement.HasOne(m => m.Item).WithMany().HasForeignKey(m => m.ItemId).OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<IdentityUser>().WithMany().HasForeignKey(m => m.ActorId).OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<PurchaseRequest>().WithMany().HasForeignKey(m => m.PurchaseRequestId).OnDelete(DeleteBehavior.Restrict);
            // One receipt movement per request (BR-05, BR-11).
            movement.HasIndex(m => m.PurchaseRequestId).IsUnique().HasFilter("\"Action\" = 'Receipt'");
            movement.ToTable(t =>
            {
                t.HasCheckConstraint("CK_StockMovements_QuantityDelta", "\"QuantityDelta\" <> 0");
                t.HasCheckConstraint("CK_StockMovements_Action", "\"Action\" IN ('Opening', 'Receipt', 'Issue')");
                // SQLite ignores HasMaxLength, so the database enforces reason length here (BR-10).
                t.HasCheckConstraint("CK_StockMovements_Reason",
                    "\"Reason\" IS NULL OR length(trim(\"Reason\")) BETWEEN 1 AND 500");
                t.HasCheckConstraint("CK_StockMovements_IssueReason", "\"Action\" <> 'Issue' OR \"Reason\" IS NOT NULL");
                // A NULL reference would bypass the one-receipt-per-request index.
                t.HasCheckConstraint("CK_StockMovements_ReceiptRequest",
                    "\"Action\" <> 'Receipt' OR \"PurchaseRequestId\" IS NOT NULL");
            });
        });

        builder.Entity<RequestEvent>(requestEvent =>
        {
            requestEvent.Property(e => e.Action).HasConversion<string>().HasMaxLength(16);
            requestEvent.Property(e => e.Note).HasMaxLength(500);
            requestEvent.HasOne<PurchaseRequest>().WithMany().HasForeignKey(e => e.PurchaseRequestId).OnDelete(DeleteBehavior.Restrict);
            requestEvent.HasOne<IdentityUser>().WithMany().HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.Restrict);
            requestEvent.ToTable(t =>
                t.HasCheckConstraint("CK_RequestEvents_Action", "\"Action\" IN ('Created', 'Approved', 'Rejected', 'Received')"));
        });
    }
}
