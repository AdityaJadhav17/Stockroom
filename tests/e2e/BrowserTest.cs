using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

// Each test starts its own application process and browser, so tests run one at a time.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Stockroom.E2ETests;

// Each test starts its own application and seeded database, then opens one browser context per signed-in
// user. A failed test saves each context's trace and screenshot plus the server log.
public abstract class BrowserTest : IAsyncLifetime
{
    private readonly List<IBrowserContext> contexts = [];
    private readonly HashSet<IBrowserContext> closedContexts = [];
    private IPlaywright? playwright;
    private IBrowser? browser;

    protected StockroomApp App { get; private set; } = null!;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        App = await StockroomApp.StartAsync(Ct);
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync();
    }

    protected async Task<IPage> LogInAsync(string email)
    {
        var context = await browser!.NewContextAsync(new() { BaseURL = App.BaseUrl.ToString() });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        contexts.Add(context);
        // A test may close a context itself; cleanup then has nothing left to stop for it.
        context.Close += (_, _) => closedContexts.Add(context);
        var page = await context.NewPageAsync();

        await page.GotoAsync("/Account/Login");
        await page.GetByLabel("Email").FillAsync(email);
        await page.GetByLabel("Password").FillAsync(App.PasswordFor(email));
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard" })).ToBeVisibleAsync();
        return page;
    }

    // The Quantity cell of an inventory row, found by the item's name cell.
    protected static ILocator InventoryQuantity(IPage page, string itemName) =>
        page.GetByRole(AriaRole.Row)
            .Filter(new() { Has = page.GetByRole(AriaRole.Cell, new() { Name = itemName, Exact = true }) })
            .GetByRole(AriaRole.Cell)
            .Nth(2);

    protected static async Task ExpectStockAsync(IPage page, string itemName, string quantity)
    {
        await page.GotoAsync("/Inventory");
        await Expect(InventoryQuantity(page, itemName)).ToHaveTextAsync(quantity);
    }

    // Every step runs even if an earlier one fails, and the application process is always stopped last.
    // Errors are reported together after cleanup finishes.
    public async ValueTask DisposeAsync()
    {
        var errors = new List<Exception>();
        async Task Attempt(Func<Task> step)
        {
            try
            {
                await step();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        var failed = TestContext.Current.TestState?.Result == TestResult.Failed;
        string? artifacts = null;
        if (failed)
        {
            await Attempt(() => Task.FromResult(artifacts = ArtifactsDirectory()));
        }
        for (var i = 0; i < contexts.Count; i++)
        {
            var context = contexts[i];
            var user = $"user{i + 1}";
            if (closedContexts.Contains(context))
            {
                continue;
            }
            if (artifacts is not null)
            {
                foreach (var page in context.Pages)
                {
                    await Attempt(() => page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, $"{user}.png"), FullPage = true }));
                }
            }
            await Attempt(() => context.Tracing.StopAsync(
                artifacts is null ? new() : new() { Path = Path.Combine(artifacts, $"{user}-trace.zip") }));
            await Attempt(() => context.CloseAsync());
        }
        if (artifacts is not null && App is not null)
        {
            await Attempt(() => File.WriteAllTextAsync(Path.Combine(artifacts, "server.log"), App.ServerLog));
        }

        if (browser is not null)
        {
            await Attempt(() => browser.DisposeAsync().AsTask());
        }
        await Attempt(() =>
        {
            playwright?.Dispose();
            return Task.CompletedTask;
        });
        if (App is not null)
        {
            await Attempt(() => App.DisposeAsync().AsTask());
        }

        if (errors.Count > 0)
        {
            throw new AggregateException("Browser test cleanup failed.", errors);
        }
    }

    // STOCKROOM_E2E_ARTIFACTS overrides the default folder under the test output, which Git ignores.
    private static string ArtifactsDirectory()
    {
        var root = Environment.GetEnvironmentVariable("STOCKROOM_E2E_ARTIFACTS")
            ?? Path.Combine(AppContext.BaseDirectory, "e2e-artifacts");
        var test = TestContext.Current.Test?.TestDisplayName ?? "unknown";
        var safeName = string.Concat(test.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        var path = Path.Combine(root, $"{safeName}-{DateTime.UtcNow:yyyyMMddHHmmss}");
        Directory.CreateDirectory(path);
        return path;
    }
}
