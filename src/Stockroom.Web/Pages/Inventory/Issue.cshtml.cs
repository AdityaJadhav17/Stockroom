using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Models;
using Stockroom.Web.Services;

namespace Stockroom.Web.Pages.Inventory;

// BR-01: only managers issue stock. StockService repeats the check against database roles.
[Authorize(Roles = Roles.Manager)]
public class IssueModel(AppDbContext db, StockService stock) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public InventoryItem Item { get; private set; } = null!;

    public class InputModel
    {
        public int? Quantity { get; set; }
        public string? Reason { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int itemId) =>
        await LoadItemAsync(itemId) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(int itemId)
    {
        if (!await LoadItemAsync(itemId))
        {
            return NotFound();
        }
        // Model binding reports fractional, non-numeric, and Int32-overflowing quantities first.
        if (ModelState.IsValid)
        {
            AddError(nameof(Input.Quantity), PurchaseRules.QuantityError(Input.Quantity));
            AddError(nameof(Input.Reason), PurchaseRules.ReasonError(Input.Reason));
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await stock.IssueAsync(itemId, User.FindFirstValue(ClaimTypes.NameIdentifier)!, Input.Quantity, Input.Reason);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await LoadItemAsync(itemId);
            return Page();
        }
        TempData["Message"] = result.Message;
        return RedirectToPage("/Inventory/Index");
    }

    private void AddError(string field, string? error)
    {
        if (error is not null)
        {
            ModelState.AddModelError($"{nameof(Input)}.{field}", error);
        }
    }

    private async Task<bool> LoadItemAsync(int itemId)
    {
        Item = (await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == itemId))!;
        return Item is not null;
    }
}
