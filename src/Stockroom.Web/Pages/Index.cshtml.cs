using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Models;
using Stockroom.Web.Services;

namespace Stockroom.Web.Pages;

public class IndexModel(AppDbContext db, PurchaseService purchases) : PageModel
{
    public List<InventoryItem> LowStock { get; private set; } = [];
    public List<RequestView> Pending { get; private set; } = [];

    public async Task OnGetAsync()
    {
        // BR-09: an item at or below its reorder threshold is low stock.
        LowStock = await db.InventoryItems.AsNoTracking()
            .Where(i => i.Quantity <= i.ReorderThreshold)
            .OrderBy(i => i.Name)
            .ToListAsync();
        Pending = await purchases
            .VisibleRequests(User.FindFirstValue(ClaimTypes.NameIdentifier)!, status: RequestStatus.Pending)
            .ToListAsync();
    }
}
