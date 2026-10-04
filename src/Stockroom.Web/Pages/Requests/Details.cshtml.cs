using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Models;
using Stockroom.Web.Services;

namespace Stockroom.Web.Pages.Requests;

public class DetailsModel(PurchaseService purchases) : PageModel
{
    public RequestView PurchaseRequest { get; private set; } = null!;

    // A member requesting another member's request receives 404, which does not reveal that it exists.
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var request = await purchases
            .VisibleRequests(User.FindFirstValue(ClaimTypes.NameIdentifier)!, User.IsInRole(Roles.Manager), requestId: id)
            .SingleOrDefaultAsync();
        if (request is null)
        {
            return NotFound();
        }
        PurchaseRequest = request;
        return Page();
    }
}
