using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Services;

namespace Stockroom.Web.Pages.Requests;

// BR-02: only members who are not managers create purchase requests, so no one approves their own purchase.
[Authorize(Policy = PurchaseRules.RequesterPolicy)]
public class NewModel(AppDbContext db, PurchaseService purchases) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> Items { get; private set; } = [];

    public class InputModel
    {
        public int ItemId { get; set; }
        public int? Quantity { get; set; }
        public string? Reason { get; set; }
    }

    public async Task OnGetAsync(int? itemId)
    {
        Input.ItemId = itemId ?? 0;
        await LoadItemsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Model binding reports fractional or non-numeric quantities before the service rules run.
        if (ModelState.IsValid)
        {
            AddError(nameof(Input.Quantity), PurchaseRules.QuantityError(Input.Quantity));
            AddError(nameof(Input.Reason), PurchaseRules.ReasonError(Input.Reason));
        }
        if (ModelState.IsValid)
        {
            var result = await purchases.CreateAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!, Input.ItemId, Input.Quantity, Input.Reason);
            if (result.Succeeded)
            {
                TempData["Message"] = result.Message;
                return RedirectToPage("/Requests/Details", new { id = result.RequestId });
            }
            ModelState.AddModelError(string.Empty, result.Message);
        }
        await LoadItemsAsync();
        return Page();
    }

    private void AddError(string field, string? error)
    {
        if (error is not null)
        {
            ModelState.AddModelError($"{nameof(Input)}.{field}", error);
        }
    }

    private async Task LoadItemsAsync() =>
        Items = await db.InventoryItems.AsNoTracking().OrderBy(i => i.Name)
            .Select(i => new SelectListItem($"{i.Name} ({i.Quantity} {i.Unit} in stock)", i.Id.ToString()))
            .ToListAsync();
}
