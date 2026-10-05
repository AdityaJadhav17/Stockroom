using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web;
using Stockroom.Web.Data;
using Stockroom.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Read the connection string when the context is created so test hosts can override it.
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseSqlite(services.GetRequiredService<IConfiguration>().GetConnectionString("Stockroom")));

// No token providers or account pages: public registration and password recovery are out of scope.
// The lockout threshold (SessionPolicy.LockoutThreshold) exceeds the attempts one client can make within one
// rate-limit window; starting from no failed attempts, one client needs about 100 seconds to lock an account.
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = SessionPolicy.LockoutThreshold;
    })
    .AddEntityFrameworkStores<AppDbContext>();
// New and upgraded password hashes use PasswordHashing.Iterations (release security review R-04).
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = PasswordHashing.Iterations);

// Sessions: re-check the security stamp on every request, so logout (which rotates the stamp) revokes copied
// cookies and role changes apply on the next request. The cost is one user lookup per authenticated request.
// Rotating the stamp signs the account out in every browser. Active sessions still renew while in use.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.ConfigureApplicationCookie(options => options.ExpireTimeSpan = SessionPolicy.IdleTimeout);
builder.Services.AddAntiforgery(options =>
{
    // SecurityHeaders sends a stricter X-Frame-Options value on every response.
    options.SuppressXFrameOptionsHeader = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Login throttling: LoginPermitLimit login POSTs per client address per sliding window (one minute by default, six segments;
// segment recycling lets up to twice that fit within one window); GETs are not limited. Clients behind one shared address share the limit.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Records the rejection as a security event; the status code page still renders the response.
    options.OnRejected = (context, _) =>
    {
        var http = context.HttpContext;
        http.RequestServices.GetRequiredService<ILogger<SecurityEvents>>()
            .LoginThrottled(SecurityEvents.ClientAddress(http), http.Request.Method, http.Request.Path.Value ?? "/");
        return ValueTask.CompletedTask;
    };
    options.AddPolicy(SessionPolicy.LoginRateLimit, context => HttpMethods.IsPost(context.Request.Method)
        ? RateLimitPartition.GetSlidingWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => SessionPolicy.LoginLimiterOptions(context.RequestServices.GetRequiredService<IConfiguration>()))
        : RateLimitPartition.GetNoLimiter("not-a-login-attempt"));
});

builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(PurchaseRules.RequesterPolicy, policy => policy.RequireAssertion(c => PurchaseRules.CanRequestPurchases(c.User)));
// Logs authorization denials as security events before the default redirect to the access-denied page.
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, SecurityEventAuthorizationHandler>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

// Development-only demo data: `seed` preserves existing records; `seed --reset` deletes the local database first.
if (args.Contains("seed"))
{
    return await DemoSeeder.RunCommandAsync(app.Services, app.Environment, args.Contains("--reset"), Console.Out, Console.Error);
}

// Security headers apply to every response, including both error pages. In every environment, unhandled
// exceptions and empty error responses render /Error without diagnostic details, and the exception handler
// middleware logs the exception. If /Error itself fails, the last-resort page answers instead.
app.UseSecurityHeaders();
app.UseLastResortErrorPage();
app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
return 0;
