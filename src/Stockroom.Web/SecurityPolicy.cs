using System.Threading.RateLimiting;

namespace Stockroom.Web;

// Session and login-throttling settings (security review F-02, F-03).
public static class SessionPolicy
{
    // Sliding idle timeout: a session that makes no request for this long expires.
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(1);

    public const string LoginRateLimit = "login";
    public const int LoginPermitLimit = 5;
    public const int LoginWindowSegments = 6;

    // Window length in seconds; tests shorten it.
    public const string LoginWindowSetting = "Security:LoginRateLimitWindowSeconds";

    // A sliding window returns a segment's permits when that segment leaves the window. Attempts made at the end
    // of a segment are therefore returned after (window - one segment), not a full window. So one client can make
    // up to 2 x LoginPermitLimit attempts within one window, and attempt 2 x LoginPermitLimit + 1 cannot come
    // sooner than 2 x (window - one segment) after the first: about 100 seconds with the defaults, for an account with no earlier failed attempts. The lockout
    // threshold sits just above that burst.
    public const int LockoutThreshold = 2 * LoginPermitLimit + 1;

    public static SlidingWindowRateLimiterOptions LoginLimiterOptions(IConfiguration configuration) => new()
    {
        PermitLimit = LoginPermitLimit,
        Window = TimeSpan.FromSeconds(configuration.GetValue(LoginWindowSetting, 60)),
        SegmentsPerWindow = LoginWindowSegments,
        QueueLimit = 0,
    };
}

// Response headers for every response, including static assets and error pages (security review F-04, F-09).
// The pages use no scripts, inline styles, or third-party resources, so the policy allows only this origin.
public static class SecurityHeaders
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XFrameOptions = "DENY";
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "same-origin";
                return Task.CompletedTask;
            });
            await next();
        });
}

// Last-resort handler outside UseExceptionHandler. The /Error page re-runs the pipeline, including authentication,
// which reads the database; if that also fails, the exception handler rethrows and, in Development, the framework's
// developer exception page would show the details. This handler logs the exception and returns a fixed page that
// needs no database, authentication, or Razor rendering.
public static class LastResortErrorPage
{
    public const string Html = """
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Something went wrong - Stockroom</title><link rel="stylesheet" href="/css/site.css"></head>
        <body><header class="site-header"><div class="header-inner"><span class="brand">Stockroom</span></div></header><main id="main"><div class="narrow card"><h1>Something went wrong</h1><p>The server could not complete the request, and the error was recorded in the server log. If you were saving a change, open the record again to check whether it was saved before retrying.</p><a class="button secondary" href="/">Return to the dashboard</a></div></main></body>
        </html>
        """;

    public static IApplicationBuilder UseLastResortErrorPage(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex) when (!context.Response.HasStarted)
            {
                context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(LastResortErrorPage).FullName!)
                    .LogError(ex, "Unhandled exception after the error page could not be rendered.");
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsync(Html);
            }
        });
}
