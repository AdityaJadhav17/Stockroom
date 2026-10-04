using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Models;
using Stockroom.Web.Services;

namespace Stockroom.Web.Pages.Requests;

public class IndexModel(PurchaseService purchases) : PageModel
{
    public List<RequestView> Requests { get; private set; } = [];

    public async Task OnGetAsync() =>
        Requests = await purchases
            .VisibleRequests(User.FindFirstValue(ClaimTypes.NameIdentifier)!, User.IsInRole(Roles.Manager))
            .ToListAsync();
}
