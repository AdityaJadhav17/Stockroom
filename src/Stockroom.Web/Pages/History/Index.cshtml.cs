using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Models;
using Stockroom.Web.Services;

namespace Stockroom.Web.Pages.History;

// Read-only: the page has no POST handlers, so history records cannot be edited or deleted here.
public class IndexModel(AppDbContext db, PurchaseService purchases, StockService stock) : PageModel
{
    public List<RequestEventView> RequestEvents { get; private set; } = [];
    public List<MovementView> Movements { get; private set; } = [];
    public bool IsManager { get; private set; }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        // The database role decides, the same source as the history queries.
        IsManager = await db.HasRoleAsync(userId, Roles.Manager);
        RequestEvents = await purchases.RequestHistory(userId).ToListAsync();
        if (IsManager)
        {
            Movements = await stock.MovementHistory(userId).ToListAsync();
        }
    }
}
