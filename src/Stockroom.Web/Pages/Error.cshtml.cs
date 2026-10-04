using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Stockroom.Web.Pages;

// Shown for unhandled exceptions (500) and empty error responses such as 404 and 400. The original request
// may be a POST with a missing antiforgery token, so this page accepts any method without validating one.
[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    public int Code { get; private set; }

    public string Heading => Code switch
    {
        404 => "Page not found",
        400 => "The request could not be processed",
        _ when Code >= 500 => "Something went wrong",
        _ => "The request could not be completed",
    };

    public string Explanation => Code switch
    {
        404 => "The page does not exist, or your account cannot view it.",
        400 => "The form may have expired. Reload the page and try again.",
        _ when Code >= 500 =>
            "The server could not complete the request, and the error was recorded in the server log. " +
            "If you were saving a change, open the record again to check whether it was saved before retrying.",
        _ => "Return to the dashboard and try again.",
    };

    public void OnGet() => Code = Response.StatusCode;

    public void OnPost() => Code = Response.StatusCode;
}
