using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stockroom.Web;
using Stockroom.Web.Data;
using Stockroom.Web.Pages.Account;
using Stockroom.Web.Services;

namespace Stockroom.IntegrationTests;

// Release security review R-02: sign-in failures, lockouts, login throttling, and authorization denials reach the
// log under the security category with stable event IDs, through the application's configured log levels, and without
// passwords, cookie values, tokens, or form bodies. Responses stay generic.
public sealed class SecurityEventLoggingTests : IAsyncLifetime
{
    private const string Category = "Stockroom.Web.SecurityEvents";

    private readonly StockroomFactory factory = new();
    private readonly LogCollector logs = new();
    private WebApplicationFactory<Program> host = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.SeedAsync();
        // Same database and settings as the factory, plus a provider that records every entry the filters let through.
        host = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
    }

    public async ValueTask DisposeAsync()
    {
        await host.DisposeAsync();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task FailedSignInsAreLoggedWithoutCredentials()
    {
        var wrongPassword = $"Wrong-{Guid.NewGuid():N}-1!";
        var client = NewClient();

        var wrong = await StockroomFactory.LoginAsync(client, DemoSeeder.Member1Email, wrongPassword);
        var unknown = await StockroomFactory.LoginAsync(client, "nobody@stockroom.test", StockroomFactory.MemberPassword);

        Assert.Contains(LoginModel.InvalidLoginMessage, await wrong.Content.ReadAsStringAsync(Ct));
        Assert.Contains(LoginModel.InvalidLoginMessage, await unknown.Content.ReadAsStringAsync(Ct));
        var failures = Events(1002, "SignInFailed");
        Assert.Equal(2, failures.Count);
        Assert.Equal(await factory.UserIdAsync(DemoSeeder.Member1Email), failures[0].Values["UserId"]);
        Assert.Equal("invalid password", failures[0].Values["Reason"]);
        Assert.Equal("none", failures[1].Values["UserId"]);
        Assert.Equal("no account matches the submitted email", failures[1].Values["Reason"]);
        Assert.All(failures, e => Assert.Equal(LogLevel.Warning, e.Level));
        AssertNoSensitiveValues(wrongPassword, StockroomFactory.MemberPassword, "nobody@stockroom.test");
    }

    [Fact]
    public async Task LockoutIsLogged()
    {
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = (await users.FindByEmailAsync(DemoSeeder.Member2Email))!;
            for (var i = 0; i < SessionPolicy.LockoutThreshold - 1; i++)
            {
                await users.AccessFailedAsync(user);
            }
        }
        var member2Id = await factory.UserIdAsync(DemoSeeder.Member2Email);
        var client = NewClient();

        var locking = await StockroomFactory.LoginAsync(client, DemoSeeder.Member2Email, "Wrong-Password-1!");
        var whileLocked = await StockroomFactory.LoginAsync(client, DemoSeeder.Member2Email, StockroomFactory.MemberPassword);

        Assert.Contains(LoginModel.InvalidLoginMessage, await locking.Content.ReadAsStringAsync(Ct));
        Assert.Contains(LoginModel.InvalidLoginMessage, await whileLocked.Content.ReadAsStringAsync(Ct));
        var lockouts = Events(1003, "SignInLockedOut");
        Assert.Equal(2, lockouts.Count);
        Assert.All(lockouts, e => Assert.Equal(member2Id, e.Values["UserId"]));
        Assert.All(lockouts, e => Assert.True(DateTimeOffset.Parse(e.Values["LockoutEnd"]) > DateTimeOffset.UtcNow));
        Assert.Empty(Events(1001, "SignInSucceeded"));
        AssertNoSensitiveValues("Wrong-Password-1!", StockroomFactory.MemberPassword);
    }

    [Fact]
    public async Task SignInAndSignOutAreLogged()
    {
        var client = NewClient();

        await StockroomFactory.LoginAsync(client, DemoSeeder.Member1Email, StockroomFactory.MemberPassword);
        var token = await StockroomFactory.GetAntiforgeryTokenAsync(client, "/");
        await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }), Ct);

        var memberId = await factory.UserIdAsync(DemoSeeder.Member1Email);
        Assert.Equal(memberId, Assert.Single(Events(1001, "SignInSucceeded")).Values["UserId"]);
        var signedOut = Assert.Single(Events(1004, "SignedOut"));
        Assert.Equal(memberId, signedOut.Values["UserId"]);
        Assert.Equal("True", signedOut.Values["SessionsRevoked"]);
        AssertNoSensitiveValues(StockroomFactory.MemberPassword, token);
    }

    [Fact]
    public async Task ThrottledLoginIsLogged()
    {
        var client = NewClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < SessionPolicy.LoginPermitLimit + 1; i++)
        {
            statuses.Add((await StockroomFactory.LoginAsync(client, DemoSeeder.ManagerEmail, "Wrong-Password-1!")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        var throttled = Assert.Single(Events(1005, "LoginThrottled"));
        Assert.Equal("POST", throttled.Values["Method"]);
        Assert.Equal("/Account/Login", throttled.Values["Path"]);
        Assert.Equal(SessionPolicy.LoginPermitLimit, Events(1002, "SignInFailed").Count);
        AssertNoSensitiveValues("Wrong-Password-1!");
    }

    [Fact]
    public async Task PageAuthorizationDenialsAreLogged()
    {
        var member = NewClient();
        await StockroomFactory.LoginAsync(member, DemoSeeder.Member1Email, StockroomFactory.MemberPassword);
        var manager = NewClient();
        await StockroomFactory.LoginAsync(manager, DemoSeeder.ManagerEmail, StockroomFactory.ManagerPassword);

        var issuePage = await member.GetAsync("/Inventory/Issue/1", Ct);
        var newRequest = await StockroomFactory.PostFormAsync(manager, "/Requests/New", new()
        {
            ["Input.ItemId"] = "1",
            ["Input.Quantity"] = "1",
            ["Input.Reason"] = "Logging probe",
        });

        Assert.Equal("/Account/AccessDenied", issuePage.Headers.Location?.AbsolutePath);
        Assert.Equal("/Account/AccessDenied", newRequest.Headers.Location?.AbsolutePath);
        var denials = Events(1006, "AccessDenied");
        Assert.Equal(2, denials.Count);
        Assert.Equal(await factory.UserIdAsync(DemoSeeder.Member1Email), denials[0].Values["UserId"]);
        Assert.Equal(("GET", "/Inventory/Issue/1", "role Manager"), (denials[0].Values["Method"], denials[0].Values["Path"], denials[0].Values["Requirement"]));
        Assert.Equal(await factory.UserIdAsync(DemoSeeder.ManagerEmail), denials[1].Values["UserId"]);
        Assert.Equal(("POST", "/Requests/New", $"policy {PurchaseRules.RequesterPolicy}"), (denials[1].Values["Method"], denials[1].Values["Path"], denials[1].Values["Requirement"]));
        AssertNoSensitiveValues(StockroomFactory.MemberPassword, StockroomFactory.ManagerPassword, "Logging probe");
    }

    [Fact]
    public async Task ServicePermissionRefusalsAreLogged()
    {
        var memberId = await factory.UserIdAsync(DemoSeeder.Member1Email);
        var managerId = await factory.UserIdAsync(DemoSeeder.ManagerEmail);
        var itemId = (await factory.ItemAsync("FIL-PLA-175")).Id;
        await using var scope = host.Services.CreateAsyncScope();
        var purchases = scope.ServiceProvider.GetRequiredService<PurchaseService>();
        var stock = scope.ServiceProvider.GetRequiredService<StockService>();
        var requestId = (await purchases.CreateAsync(memberId, itemId, 1, "Logging probe")).RequestId!.Value;

        OperationResult[] results =
        [
            await purchases.ApproveAsync(requestId, memberId),
            await purchases.ReceiveAsync(requestId, memberId),
            await purchases.CreateAsync(managerId, itemId, 1, "Manager purchase"),
            await stock.IssueAsync(itemId, memberId, 1, "Member issue"),
        ];

        Assert.All(results, r => Assert.Equal(ServiceGuard.ForbiddenMessage, r.Message));
        var refusals = Events(1007, "PermissionRefused");
        Assert.Equal(
            [
                (memberId, $"review purchase request {requestId}", "the Manager role"),
                (memberId, $"receive purchase request {requestId}", "the Manager role"),
                (managerId, "create a purchase request", "the Member role without the Manager role"),
                (memberId, $"issue stock of item {itemId}", "the Manager role"),
            ],
            refusals.Select(e => (e.Values["UserId"], e.Values["Action"], e.Values["Requirement"])).ToArray());
        AssertNoSensitiveValues("Manager purchase", "Member issue");
    }

    private HttpClient NewClient() => host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // Security events with the given ID, in the order they were logged. The ID and name are stable for log queries.
    private List<LogRecord> Events(int id, string name) =>
        logs.Records.Where(r => r.Category == Category && r.EventId.Id == id)
            .Select(r =>
            {
                Assert.Equal(name, r.EventId.Name);
                return r;
            })
            .ToList();

    // Applies to every entry from every category recorded during the test, not only security events.
    // CfDJ8 is the prefix of ASP.NET Core Data Protection payloads, which includes authentication cookies and
    // antiforgery tokens.
    private void AssertNoSensitiveValues(params string[] values)
    {
        Assert.NotEmpty(logs.Records);
        foreach (var record in logs.Records)
        {
            var text = record.Text;
            Assert.DoesNotContain("CfDJ8", text, StringComparison.Ordinal);
            Assert.DoesNotContain("__RequestVerificationToken", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Input.Password", text, StringComparison.Ordinal);
            Assert.DoesNotContain(".AspNetCore.Identity", text, StringComparison.Ordinal);
            Assert.DoesNotContain(".AspNetCore.Antiforgery", text, StringComparison.Ordinal);
            foreach (var value in values)
            {
                Assert.DoesNotContain(value, text, StringComparison.Ordinal);
            }
        }
    }

    private sealed record LogRecord(string Category, LogLevel Level, EventId EventId, string Message, IReadOnlyDictionary<string, string> Values)
    {
        public string Text => Message + " " + string.Join(" ", Values.Values);
    }

    // Records every entry that the application's configured log filters pass to providers.
    private sealed class LogCollector : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogRecord> records = new();

        public IReadOnlyList<LogRecord> Records => [.. records];

        public ILogger CreateLogger(string categoryName) => new Collector(categoryName, records);

        public void Dispose()
        {
        }

        private sealed class Collector(string category, ConcurrentQueue<LogRecord> records) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? pairs.Where(p => p.Key != "{OriginalFormat}").ToDictionary(p => p.Key, p => p.Value?.ToString() ?? "")
                    : new Dictionary<string, string>();
                records.Enqueue(new LogRecord(category, logLevel, eventId, formatter(state, exception) + " " + exception, values));
            }
        }
    }
}
