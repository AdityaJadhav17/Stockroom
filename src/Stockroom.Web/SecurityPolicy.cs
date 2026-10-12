using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Stockroom.Web;

// Session and login-throttling settings (security review F-02, F-03).
public static class SessionPolicy
{
    // Sliding idle timeout: a session that makes no request for this long expires.
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(1);

    // Absolute lifetime: a session ends this long after sign-in even while it stays active (security review F-02).
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(8);
    private const string SignedInItem = "stockroom.signed-in";

    // Records the sign-in time in the cookie's protected properties, which renewals keep, and rejects a cookie
    // older than AbsoluteLifetime or without that time before the security-stamp check runs.
    public static void EnforceAbsoluteLifetime(CookieAuthenticationOptions options)
    {
        var signingIn = options.Events.OnSigningIn;
        options.Events.OnSigningIn = context =>
        {
            context.Properties.Items.TryAdd(SignedInItem,
                (context.Options.TimeProvider ?? TimeProvider.System).GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
            return signingIn(context);
        };
        var validatePrincipal = options.Events.OnValidatePrincipal;
        options.Events.OnValidatePrincipal = async context =>
        {
            var now = (context.Options.TimeProvider ?? TimeProvider.System).GetUtcNow();
            if (!context.Properties.Items.TryGetValue(SignedInItem, out var value)
                || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var signedIn)
                || now - signedIn >= AbsoluteLifetime)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(context.Scheme.Name);
                return;
            }
            await validatePrincipal(context);
        };
    }

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

// Password hashing (release security review R-04). Identity's V3 format uses PBKDF2-HMAC-SHA512; its default of
// 100,000 iterations is below the OWASP Password Storage Cheat Sheet figure of 220,000 for that function (checked
// 2026-10-05). A hash stored with fewer iterations still verifies, and Identity replaces it at the next successful
// sign-in.
public static class PasswordHashing
{
    public const int Iterations = 220_000;
}

// A failed check against a hash stored with fewer iterations runs the missing PBKDF2 iterations as padding, so a
// wrong password costs the same for older accounts as for current ones (security review F-06). A successful check
// needs no padding: Identity then replaces the hash.
public class ConstantCostPasswordHasher(IOptions<PasswordHasherOptions> options) : PasswordHasher<IdentityUser>(options)
{
    private static readonly byte[] PaddingSalt = new byte[16];
    private readonly int iterations = options.Value.IterationCount;

    public override PasswordVerificationResult VerifyHashedPassword(IdentityUser user, string hashedPassword, string providedPassword)
    {
        var result = base.VerifyHashedPassword(user, hashedPassword, providedPassword);
        var padding = PaddingIterations(hashedPassword);
        if (result == PasswordVerificationResult.Failed && padding > 0)
        {
            Pad(providedPassword, padding);
        }
        return result;
    }

    protected virtual void Pad(string providedPassword, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(providedPassword, PaddingSalt, iterations, HashAlgorithmName.SHA512, 32);

    // Iterations short of the configured count. Identity's V3 format stores the count as a big-endian uint32 at
    // bytes 5 to 8; any other or unreadable format counts as zero iterations.
    public int PaddingIterations(string hashedPassword)
    {
        var bytes = new byte[hashedPassword.Length];
        var stored = Convert.TryFromBase64String(hashedPassword, bytes, out var length) && length >= 9 && bytes[0] == 0x01
            ? BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4))
            : 0u;
        return stored >= (uint)iterations ? 0 : iterations - (int)stored;
    }
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

// The application serves only GET, HEAD, and POST (security review F-14). Other methods, including TRACE and
// OPTIONS, receive 405 before routing, authentication, or the error pages run.
public static class AllowedMethods
{
    public const string Allow = "GET, HEAD, POST";

    public static IApplicationBuilder UseAllowedMethods(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var method = context.Request.Method;
            if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsPost(method))
            {
                await next();
                return;
            }
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.Headers.Allow = Allow;
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
