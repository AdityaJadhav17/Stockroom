using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Stockroom.Web.Pages.Account;

// Logout accepts only an antiforgery-protected POST; a GET returns to the dashboard.
// Rotating the security stamp revokes every copy of this account's cookies, including other browsers,
// because the stamp is validated on each request. If rotation fails, this browser is still signed out,
// and the user is told that other sessions may remain signed in.
public class LogoutModel(
    SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager, ILogger<LogoutModel> logger) : PageModel
{
    public const string RevocationFailedMessage =
        "You are signed out of this browser, but other sessions for this account could not be ended. " +
        "Log in and log out again, or ask the manager for help.";

    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await RevokeSessionsAsync())
        {
            TempData["Error"] = RevocationFailedMessage;
        }
        await signInManager.SignOutAsync();
        return RedirectToPage("/Account/Login");
    }

    private async Task<bool> RevokeSessionsAsync()
    {
        try
        {
            // A missing user cannot pass stamp validation, so there is nothing left to revoke.
            if (await userManager.GetUserAsync(User) is not { } user)
            {
                return true;
            }
            var result = await userManager.UpdateSecurityStampAsync(user);
            if (!result.Succeeded)
            {
                logger.LogWarning("Logout could not rotate the security stamp: {Errors}",
                    string.Join(" ", result.Errors.Select(e => e.Code)));
            }
            return result.Succeeded;
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            logger.LogError(ex, "Logout could not rotate the security stamp.");
            return false;
        }
    }
}
