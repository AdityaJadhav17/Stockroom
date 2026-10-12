using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Models;

namespace Stockroom.Web.Pages.Inventory;

public class IndexModel(AppDbContext db) : PageModel
{
    // Item names are short; longer search terms are cut to this length before the query.
    public const int MaxSearchLength = 100;

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    public List<InventoryItem> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var items = db.InventoryItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            // Case-insensitive for ASCII names; instr() treats % and _ literally.
            var term = Search.Trim().ToLower();
            term = term[..Math.Min(term.Length, MaxSearchLength)];
            items = items.Where(i => i.Name.ToLower().Contains(term));
        }
        Items = await items.OrderBy(i => i.Name).ToListAsync();
    }
}
