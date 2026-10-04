using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Services;

namespace Stockroom.Web.Pages.Requests;

public class DetailsModel(PurchaseService purchases) : PageModel
{
    public RequestView PurchaseRequest { get; private set; } = null!;
    public List<RequestEventView> Events { get; private set; } = [];

    // A member requesting another member's request receives 404, which does not reveal that it exists.
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var request = await purchases.VisibleRequests(userId, requestId: id).SingleOrDefaultAsync();
        if (request is null)
        {
            return NotFound();
        }
        PurchaseRequest = request;
        Events = await purchases.RequestHistory(userId, requestId: id).ToListAsync();
        return Page();
    }
}
