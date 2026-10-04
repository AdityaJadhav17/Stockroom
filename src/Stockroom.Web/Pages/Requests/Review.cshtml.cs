using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Stockroom.Web.Models;
using Stockroom.Web.Services;

namespace Stockroom.Web.Pages.Requests;

// Manager actions live on their own page so the role check covers every handler (BR-01).
[Authorize(Roles = Roles.Manager)]
public class ReviewModel(PurchaseService purchases) : PageModel
{
    public IActionResult OnGet(int id) => RedirectToPage("/Requests/Details", new { id });

    public async Task<IActionResult> OnPostApproveAsync(int id) =>
        Done(id, await purchases.ApproveAsync(id, ActorId));

    public async Task<IActionResult> OnPostRejectAsync(int id, string? reason) =>
        Done(id, await purchases.RejectAsync(id, ActorId, reason));

    public async Task<IActionResult> OnPostReceiveAsync(int id) =>
        Done(id, await purchases.ReceiveAsync(id, ActorId));

    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private IActionResult Done(int id, OperationResult result)
    {
        TempData[result.Succeeded ? "Message" : "Error"] = result.Message;
        return RedirectToPage("/Requests/Details", new { id });
    }
}
