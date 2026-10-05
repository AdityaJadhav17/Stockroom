using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Stockroom.Web.Pages.Account;

// Login POSTs are rate-limited per client address (SessionPolicy); GETs are not.
[EnableRateLimiting(SessionPolicy.LoginRateLimit)]
public class LoginModel(SignInManager<IdentityUser> signInManager, ILogger<SecurityEvents> securityLog) : PageModel
{
    public const string InvalidLoginMessage = "Invalid email or password.";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    public void OnGet(string? returnUrl) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // The same steps as PasswordSignInAsync(userName, ...), with the account in hand for the security log.
        var client = SecurityEvents.ClientAddress(HttpContext);
        var user = await signInManager.UserManager.FindByNameAsync(Input.Email);
        var result = user is null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signInManager.PasswordSignInAsync(user, Input.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            // The same message covers unknown accounts, wrong passwords, and lockout. The log records the account ID,
            // never the password, and omits the submitted email when no account matches it.
            if (user is null)
            {
                securityLog.SignInFailed("none", client, "no account matches the submitted email");
            }
            else if (result.IsLockedOut)
            {
                var lockoutEnd = await signInManager.UserManager.GetLockoutEndDateAsync(user);
                securityLog.SignInLockedOut(user.Id, client, lockoutEnd?.ToString("O") ?? "unknown");
            }
            else
            {
                securityLog.SignInFailed(user.Id, client, result.IsNotAllowed ? "sign-in not allowed" : "invalid password");
            }
            ModelState.AddModelError(string.Empty, InvalidLoginMessage);
            return Page();
        }

        securityLog.SignInSucceeded(user!.Id, client);
        return LocalRedirect(returnUrl is not null && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
