using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stockroom.Web.Data;
using Stockroom.Web.Models;
using Stockroom.Web.Services;

namespace Stockroom.IntegrationTests;

// Controlled responses for database failures and error status codes: details go to the log, not the page.
public sealed class ErrorHandlingTests : IAsyncLifetime
{
    private const string ForcedFailure = "forced failure for the error test";

    private readonly StockroomFactory factory = new();
    private readonly LogCollector logs = new();
    private WebApplicationFactory<Program> host = null!;
    private int filamentId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.SeedAsync();
        filamentId = (await factory.ItemAsync("FIL-PLA-175")).Id;
        // Same database and settings as the factory, plus a logger provider that records errors.
        host = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
    }

    public async ValueTask DisposeAsync()
    {
        await host.DisposeAsync();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task FailedReceiptShowsControlledMessageLogsDetailsAndChangesNothing()
    {
        var id = await CreateApprovedRequestAsync();
        await ForceFailureOnReceivedEventAsync();
        var before = await factory.SnapshotAsync(id);
        var client = await LogInAsync(DemoSeeder.ManagerEmail, StockroomFactory.ManagerPassword);

        var post = await StockroomFactory.PostFormAsync(client, $"/Requests/Review/{id}?handler=Receive");
        var page = await client.GetStringAsync(post.Headers.Location!.OriginalString, Ct);

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains(WebUtility.HtmlEncode(ServiceGuard.DatabaseErrorMessage), page);
        Assert.DoesNotContain(ForcedFailure, page);
        Assert.DoesNotContain("Nothing was changed", page);
        Assert.Equal(before, await factory.SnapshotAsync(id));
        Assert.Contains(logs.Errors, e => e.Contains(ForcedFailure));
    }

    [Fact]
    public async Task UnhandledDatabaseErrorShowsErrorPageWithoutDiagnostics()
    {
        var client = await LogInAsync(DemoSeeder.Member1Email, StockroomFactory.MemberPassword);
        await factory.RunAsync((_, db) => db.Database.ExecuteSqlRawAsync("ALTER TABLE InventoryItems RENAME TO InventoryItemsMissing", Ct));

        var response = await client.GetAsync("/Inventory", Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("<h1>Something went wrong</h1>", html);
        Assert.DoesNotContain("SqliteException", html);
        Assert.DoesNotContain("InventoryItems", html);
        Assert.Contains(logs.Errors, e => e.Contains("no such table: InventoryItems"));
    }

    [Theory]
    [InlineData("/Requests/Details/99999")]
    [InlineData("/No/Such/Page")]
    public async Task MissingPageShowsNotFoundPage(string path)
    {
        var client = await LogInAsync(DemoSeeder.Member1Email, StockroomFactory.MemberPassword);

        var response = await client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<h1>Page not found</h1>", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task OtherMembersRequestShowsTheSameNotFoundPage()
    {
        var otherId = await factory.UserIdAsync(DemoSeeder.Member2Email);
        var id = (await factory.RunAsync((s, _) => s.CreateAsync(otherId, filamentId, 2, "Other member's request"))).RequestId!.Value;
        var client = await LogInAsync(DemoSeeder.Member1Email, StockroomFactory.MemberPassword);

        var response = await client.GetAsync($"/Requests/Details/{id}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<h1>Page not found</h1>", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task MissingAntiforgeryTokenShowsBadRequestPage()
    {
        var client = await LogInAsync(DemoSeeder.Member1Email, StockroomFactory.MemberPassword);

        var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent([]), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("<h1>The request could not be processed</h1>", await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<HttpClient> LogInAsync(string email, string password)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await StockroomFactory.LoginAsync(client, email, password);
        return client;
    }

    private async Task<int> CreateApprovedRequestAsync()
    {
        var memberId = await factory.UserIdAsync(DemoSeeder.Member1Email);
        var managerId = await factory.UserIdAsync(DemoSeeder.ManagerEmail);
        var id = (await factory.RunAsync((s, _) => s.CreateAsync(memberId, filamentId, 5, "Robotics workshop"))).RequestId!.Value;
        await factory.RunAsync((s, _) => s.ApproveAsync(id, managerId));
        return id;
    }

    private Task<int> ForceFailureOnReceivedEventAsync() =>
        factory.RunAsync((_, db) => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER fail_received_event BEFORE INSERT ON RequestEvents WHEN NEW.Action = 'Received' " +
            $"BEGIN SELECT RAISE(ABORT, '{ForcedFailure}'); END;", Ct));

    // Records error-level log entries with their exception text.
    private sealed class LogCollector : ILoggerProvider
    {
        public ConcurrentQueue<string> Errors { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Collector(Errors);

        public void Dispose()
        {
        }

        private sealed class Collector(ConcurrentQueue<string> errors) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                {
                    errors.Enqueue($"{formatter(state, exception)} {exception}");
                }
            }
        }
    }
}
