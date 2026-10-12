using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stockroom.Web;
using Stockroom.Web.Data;
using Stockroom.Web.Models;

namespace Stockroom.IntegrationTests;

// Regressions for the M7 security review: F-02 and F-08 (sessions), F-03 (login throttling),
// F-04 and F-09 (response headers and cookies), F-06 (login timing), and F-14 (HTTP methods).
[Collection("Security timing")]
public sealed class SecurityHardeningTests : IAsyncLifetime
{
    private readonly StockroomFactory factory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.SeedAsync();

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Theory]
    [InlineData("/Account/Login", false)]
    [InlineData("/css/site.css", false)]
    [InlineData("/No/Such/Page", true)]
    [InlineData("/", true)]
    [InlineData("/Requests/Details/1", true)]
    [InlineData("/Inventory/Issue/1", true)]
    public async Task EveryResponseCarriesSecurityHeaders(string path, bool asManager)
    {
        await factory.RunAsync(async (s, _) => (await s.CreateAsync(
            await factory.UserIdAsync(DemoSeeder.Member1Email), (await factory.ItemAsync("FIL-PLA-175")).Id, 1, "Header probe")).RequestId);
        var client = asManager ? await factory.LoggedInClientAsync(DemoSeeder.ManagerEmail) : factory.CreateBrowserClient();

        var response = await client.GetAsync(path, Ct);

        Assert.Equal(["DENY"], response.Headers.GetValues("X-Frame-Options"));
        Assert.Equal([SecurityHeaders.ContentSecurityPolicy], response.Headers.GetValues("Content-Security-Policy"));
        Assert.Equal(["nosniff"], response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(["same-origin"], response.Headers.GetValues("Referrer-Policy"));
    }

    [Fact]
    public async Task LogoutRevokesACopiedSessionCookie()
    {
        var (client, cookies) = NewCookieClient();
        await StockroomFactory.LoginAsync(client, DemoSeeder.Member1Email, StockroomFactory.MemberPassword);
        var copy = Copy(cookies);
        Assert.Equal(HttpStatusCode.OK, (await CookieClient(copy).GetAsync("/", Ct)).StatusCode);

        await StockroomFactory.PostFormAsync(client, "/Account/Logout");
        var replayed = await CookieClient(Copy(copy)).GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.Redirect, replayed.StatusCode);
        Assert.Equal("/Account/Login", replayed.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task LogoutSignsTheAccountOutInEveryBrowser()
    {
        // Documented trade-off of security-stamp rotation: one logout ends all sessions for the account.
        var first = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);
        var second = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);

        await StockroomFactory.PostFormAsync(first, "/Account/Logout");
        var response = await second.GetAsync("/", Ct);

        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task RoleRemovalAppliesOnTheNextRequest()
    {
        var manager = await factory.LoggedInClientAsync(DemoSeeder.ManagerEmail);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/Inventory/Issue/1", Ct)).StatusCode);

        await factory.RemoveRoleAsync(DemoSeeder.ManagerEmail, Roles.Manager);
        var issuePage = await manager.GetAsync("/Inventory/Issue/1", Ct);
        var inventory = await manager.GetStringAsync("/Inventory", Ct);
        var history = await manager.GetStringAsync("/History", Ct);

        Assert.Equal("/Account/AccessDenied", issuePage.Headers.Location?.AbsolutePath);
        Assert.DoesNotContain("/Inventory/Issue/", inventory);
        Assert.DoesNotContain("Stock movements", history);
    }

    [Fact]
    public async Task LoginPostsAreThrottledBeforeTheAccountLocks()
    {
        var client = factory.CreateBrowserClient();
        for (var attempt = 0; attempt < SessionPolicy.LoginPermitLimit; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK,
                (await StockroomFactory.LoginAsync(client, DemoSeeder.ManagerEmail, "Wrong-Password-1!")).StatusCode);
        }

        var throttled = await StockroomFactory.LoginAsync(client, DemoSeeder.ManagerEmail, StockroomFactory.ManagerPassword);
        var loginPage = await client.GetAsync("/Account/Login", Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Contains("<h1>Too many attempts</h1>", await throttled.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var manager = (await users.FindByEmailAsync(DemoSeeder.ManagerEmail))!;
        Assert.False(await users.IsLockedOutAsync(manager));
        Assert.Equal(SessionPolicy.LoginPermitLimit, await users.GetAccessFailedCountAsync(manager));
    }

    [Fact]
    public async Task DatabaseFailureDuringAuthenticationShowsOnlyTheGenericErrorPage()
    {
        // Per-request stamp validation reads the users table, so the /Error re-execution fails too.
        var client = await factory.LoggedInClientAsync(DemoSeeder.Member1Email);
        await factory.RunAsync((_, db) => db.Database.ExecuteSqlRawAsync("ALTER TABLE AspNetUsers RENAME TO AspNetUsersMissing", Ct));

        var response = await client.GetAsync("/", Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("<h1>Something went wrong</h1>", html);
        Assert.DoesNotContain("SqliteException", html);
        Assert.DoesNotContain("AspNetUsers", html);
        Assert.DoesNotContain("Stack", html);
        Assert.Equal(["DENY"], response.Headers.GetValues("X-Frame-Options"));
    }

    [Fact]
    public async Task FailedSessionRevocationIsReportedToTheUser()
    {
        var (client, cookies) = NewCookieClient();
        await StockroomFactory.LoginAsync(client, DemoSeeder.Member1Email, StockroomFactory.MemberPassword);
        var copy = Copy(cookies);
        await factory.RunAsync((_, db) => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER fail_stamp_rotation BEFORE UPDATE ON AspNetUsers WHEN NEW.SecurityStamp <> OLD.SecurityStamp " +
            "BEGIN SELECT RAISE(ABORT, 'forced stamp failure'); END;", Ct));

        var logout = await StockroomFactory.PostFormAsync(client, "/Account/Logout");
        var loginPage = await client.GetStringAsync(logout.Headers.Location!.OriginalString, Ct);
        var thisBrowser = await client.GetAsync("/", Ct);
        var copied = await CookieClient(Copy(copy)).GetAsync("/", Ct);

        Assert.Contains(WebUtility.HtmlEncode(Stockroom.Web.Pages.Account.LogoutModel.RevocationFailedMessage), loginPage);
        Assert.DoesNotContain("forced stamp failure", loginPage);
        Assert.Equal("/Account/Login", thisBrowser.Headers.Location?.AbsolutePath);
        // The message is accurate: the copy stays valid because the stamp did not change.
        Assert.Equal(HttpStatusCode.OK, copied.StatusCode);
    }

    [Fact]
    public async Task LoginWindowBoundaryCannotDoubleTheAttemptsAllowed()
    {
        // A fixed window would allow LoginPermitLimit attempts just before its boundary and as many again just after it.
        // With a 2-second window, the fixed window resets by about 2.2 s (100 ms replenishment ticks); the sliding window
        // still counts the attempts made near 1.8 s until about 3.7 s.
        await using var host = factory.WithWebHostBuilder(builder => builder.UseSetting(SessionPolicy.LoginWindowSetting, "2"));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var statuses = new List<HttpStatusCode> { (await StockroomFactory.LoginAsync(client, DemoSeeder.ManagerEmail, "Wrong-Password-1!")).StatusCode };

        await DelayUntilAsync(clock, TimeSpan.FromMilliseconds(1700));
        for (var attempt = 1; attempt < SessionPolicy.LoginPermitLimit; attempt++)
        {
            statuses.Add((await StockroomFactory.LoginAsync(client, DemoSeeder.ManagerEmail, "Wrong-Password-1!")).StatusCode);
        }
        await DelayUntilAsync(clock, TimeSpan.FromMilliseconds(2400));
        for (var attempt = 0; attempt < SessionPolicy.LoginPermitLimit; attempt++)
        {
            statuses.Add((await StockroomFactory.LoginAsync(client, DemoSeeder.ManagerEmail, "Wrong-Password-1!")).StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var manager = (await users.FindByEmailAsync(DemoSeeder.ManagerEmail))!;
        Assert.True(statuses.Count(s => s == HttpStatusCode.OK) <= SessionPolicy.LoginPermitLimit + 1,
            $"Accepted attempts: {string.Join(",", statuses.Select(s => (int)s))}");
        Assert.InRange(await users.GetAccessFailedCountAsync(manager), SessionPolicy.LoginPermitLimit, SessionPolicy.LoginPermitLimit + 1);
        Assert.False(await users.IsLockedOutAsync(manager));
    }

    [Fact]
    public async Task OneClientCannotReachTheLockoutWithinTheGuaranteedInterval()
    {
        // Sliding-window segments return their permits early, so one client can send 2 x LoginPermitLimit attempts
        // within one window. The (2 x LoginPermitLimit + 1)th cannot come before 2 x (window - one segment):
        // 3.33 s for a 2-second window with six segments. Hammer the login for 3 s and require no lockout.
        await using var host = factory.WithWebHostBuilder(builder => builder.UseSetting(SessionPolicy.LoginWindowSetting, "2"));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var accepted = 0;
        var attempts = 0;
        while (clock.Elapsed < TimeSpan.FromSeconds(3))
        {
            attempts++;
            if ((await StockroomFactory.LoginAsync(client, DemoSeeder.ManagerEmail, "Wrong-Password-1!")).StatusCode == HttpStatusCode.OK)
            {
                accepted++;
            }
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var manager = (await users.FindByEmailAsync(DemoSeeder.ManagerEmail))!;
        Assert.InRange(accepted, SessionPolicy.LoginPermitLimit, SessionPolicy.LockoutThreshold - 1);
        Assert.False(await users.IsLockedOutAsync(manager), $"Locked after {accepted} accepted of {attempts} attempts.");
        Assert.True(attempts > SessionPolicy.LockoutThreshold, "The loop must try more attempts than the threshold.");
    }

    [Fact]
    public async Task AntiforgeryCookieIsSecureOverHttps()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync("/Account/Login", Ct);

        var antiforgery = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal));
        Assert.Contains("; secure", antiforgery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CookiesAreSecureOutsideDevelopment()
    {
        // Outside Development both cookies always carry Secure, so a hosted run must reach the application over HTTPS.
        await using var production = new StockroomFactory(environment: "Production");
        await production.SeedAsync();
        var client = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var page = await client.GetAsync("/Account/Login", Ct);
        var login = await StockroomFactory.LoginAsync(client, DemoSeeder.Member1Email, StockroomFactory.MemberPassword);

        var antiforgery = Assert.Single(page.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal));
        var session = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Identity.Application", StringComparison.Ordinal));
        Assert.Contains("; secure", antiforgery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; secure", session, StringComparison.OrdinalIgnoreCase);

        // Over plain HTTP the antiforgery system refuses to issue a token, so no cookie leaves without Secure.
        var overHttp = await production.CreateBrowserClient().GetAsync("/Account/Login", Ct);
        Assert.False(overHttp.Headers.TryGetValues("Set-Cookie", out var insecure) && insecure.Any(c => !c.Contains("; secure", StringComparison.OrdinalIgnoreCase)),
            "A cookie was issued without Secure over HTTP.");
    }

    [Theory]
    [InlineData("TRACE", "/")]
    [InlineData("OPTIONS", "/Account/Login")]
    [InlineData("PUT", "/Requests/Review/1")]
    [InlineData("DELETE", "/")]
    [InlineData("PATCH", "/Account/Login")]
    public async Task OnlyGetHeadAndPostAreServed(string method, string path)
    {
        var response = await factory.CreateBrowserClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), path), Ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(["GET", "HEAD", "POST"], response.Content.Headers.Allow);
        Assert.Equal(["DENY"], response.Headers.GetValues("X-Frame-Options"));
    }

    [Fact]
    public async Task UnknownEmailCostsTheSameHashCheckAsAWrongPassword()
    {
        var hasher = new CountingPasswordHasher();
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddScoped<IPasswordHasher<IdentityUser>>(_ => hasher)));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await StockroomFactory.LoginAsync(client, "nobody@stockroom.test", "Wrong-Password-1!");
        var unknownEmailChecks = hasher.Verifications;
        await StockroomFactory.LoginAsync(client, DemoSeeder.Member1Email, "Wrong-Password-1!");

        Assert.Equal(1, unknownEmailChecks);
        Assert.Equal(2, hasher.Verifications);
    }

    [Fact]
    public async Task LockedAccountCostsTheSameHashCheckAsAWrongPassword()
    {
        // Identity refuses a locked account before checking the password; the login page must still do one check.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var member = (await users.FindByEmailAsync(DemoSeeder.Member2Email))!;
            Assert.True((await users.SetLockoutEndDateAsync(member, DateTimeOffset.UtcNow.AddHours(1))).Succeeded);
        }
        var hasher = new CountingPasswordHasher();
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddScoped<IPasswordHasher<IdentityUser>>(_ => hasher)));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await StockroomFactory.LoginAsync(client, DemoSeeder.Member2Email, "Wrong-Password-1!");
        var wrongPasswordChecks = hasher.Verifications;
        await StockroomFactory.LoginAsync(client, DemoSeeder.Member2Email, StockroomFactory.MemberPassword);

        Assert.Equal(1, wrongPasswordChecks);
        Assert.Equal(2, hasher.Verifications);
    }

    [Fact]
    public async Task OlderHashesArePaddedToTheConfiguredCost()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var hasher = Assert.IsType<ConstantCostPasswordHasher>(scope.ServiceProvider.GetRequiredService<IPasswordHasher<IdentityUser>>());
        var user = new IdentityUser();
        var legacy = new PasswordHasher<IdentityUser>(Options.Create(new PasswordHasherOptions { IterationCount = 100_000 }))
            .HashPassword(user, "Older-Pass-1");

        Assert.Equal(PasswordHashing.Iterations - 100_000, hasher.PaddingIterations(legacy));
        Assert.Equal(0, hasher.PaddingIterations(hasher.HashPassword(user, "Current-Pass-1")));
        Assert.Equal(PasswordHashing.Iterations, hasher.PaddingIterations("not a stored hash"));

        var recording = new PaddingRecorder();
        Assert.Equal(PasswordVerificationResult.Failed, recording.VerifyHashedPassword(user, legacy, "Wrong-Password-1!"));
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, recording.VerifyHashedPassword(user, legacy, "Older-Pass-1"));
        Assert.Equal(PasswordVerificationResult.Failed, recording.VerifyHashedPassword(user, recording.HashPassword(user, "Current-Pass-1"), "Wrong-Password-1!"));
        // Only the failed check against the older hash is padded, by exactly the missing iterations.
        Assert.Equal([PasswordHashing.Iterations - 100_000], recording.Padded);
    }

    private sealed class PaddingRecorder()
        : ConstantCostPasswordHasher(Options.Create(new PasswordHasherOptions { IterationCount = PasswordHashing.Iterations }))
    {
        public List<int> Padded { get; } = [];

        protected override void Pad(string providedPassword, int iterations) => Padded.Add(iterations);
    }

    [Fact]
    public async Task SessionEndsAtTheAbsoluteLifetimeEvenWhileActive()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options => options.TimeProvider = clock);
            services.Configure<SecurityStampValidatorOptions>(options => options.TimeProvider = clock);
        }));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await StockroomFactory.LoginAsync(client, DemoSeeder.Member1Email, StockroomFactory.MemberPassword);

        // A request every 50 minutes keeps the one-hour idle timeout from ending the session first.
        var step = TimeSpan.FromMinutes(50);
        var elapsed = TimeSpan.Zero;
        while (elapsed + step < SessionPolicy.AbsoluteLifetime)
        {
            clock.Advance(step);
            elapsed += step;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/", Ct)).StatusCode);
        }
        clock.Advance(SessionPolicy.AbsoluteLifetime - elapsed);
        var expired = await client.GetAsync("/", Ct);

        Assert.Equal("/Account/Login", expired.Headers.Location?.AbsolutePath);
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class CountingPasswordHasher()
        : PasswordHasher<IdentityUser>(Options.Create(new PasswordHasherOptions { IterationCount = PasswordHashing.Iterations }))
    {
        private int verifications;

        public int Verifications => verifications;

        public override PasswordVerificationResult VerifyHashedPassword(IdentityUser user, string hashedPassword, string providedPassword)
        {
            Interlocked.Increment(ref verifications);
            return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    // A slow runner can reach a checkpoint late. Keep the original schedule, but never pass a negative delay.
    private static async Task DelayUntilAsync(System.Diagnostics.Stopwatch clock, TimeSpan checkpoint)
    {
        var remaining = checkpoint - clock.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, Ct);
        }
    }

    private (HttpClient Client, CookieContainer Cookies) NewCookieClient()
    {
        var cookies = new CookieContainer();
        return (CookieClient(cookies), cookies);
    }

    private HttpClient CookieClient(CookieContainer cookies) =>
        factory.CreateDefaultClient(new Uri("http://localhost"), new CookieContainerHandler(cookies));

    private static CookieContainer Copy(CookieContainer source)
    {
        var copy = new CookieContainer();
        foreach (Cookie cookie in source.GetCookies(new Uri("http://localhost")))
        {
            copy.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path, "localhost"));
        }
        return copy;
    }
}
