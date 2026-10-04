using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Stockroom.E2ETests;

// Captures every main page at desktop and phone widths, plus desktop in dark mode, against a freshly seeded
// fixture database, for UI review.
// Writes full-page screenshots and an overflow report to artifacts/ui/<label> (ignored by Git), where <label>
// comes from STOCKROOM_UI_LABEL (default "current"). It is explicit, so normal runs and CI skip it.
// Run: dotnet test --project tests/e2e/Stockroom.E2ETests.csproj -c Release --no-build -- --explicit only
//      --filter-class Stockroom.E2ETests.UiScreenshots
public sealed class UiScreenshots
{
    [Fact(Explicit = true)]
    public async Task CapturePages()
    {
        var ct = TestContext.Current.CancellationToken;
        var label = Environment.GetEnvironmentVariable("STOCKROOM_UI_LABEL") ?? "current";
        var output = Path.Combine(StockroomApp.RepositoryRoot(), "artifacts", "ui", label);
        Directory.CreateDirectory(output);
        var report = new List<string>();

        await using var app = await StockroomApp.StartAsync(ct);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();

        // One sign-in per role keeps the run under the login rate limit; each page is captured at both widths.
        async Task<IPage> OpenAsync(string? email)
        {
            var context = await browser.NewContextAsync(new() { BaseURL = app.BaseUrl.ToString() });
            var page = await context.NewPageAsync();
            if (email is not null)
            {
                await page.GotoAsync("/Account/Login");
                await page.GetByLabel("Email").FillAsync(email);
                await page.GetByLabel("Password").FillAsync(app.PasswordFor(email));
                await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard" })).ToBeVisibleAsync();
            }
            return page;
        }

        async Task Shot(IPage page, string name)
        {
            foreach (var (device, width, height, scheme) in new[]
                     {
                         ("desktop", 1280, 800, ColorScheme.Light),
                         ("mobile", 390, 844, ColorScheme.Light),
                         ("dark-desktop", 1280, 800, ColorScheme.Dark),
                     })
            {
                await page.EmulateMediaAsync(new() { ColorScheme = scheme });
                await page.SetViewportSizeAsync(width, height);
                await page.ScreenshotAsync(new()
                {
                    Path = Path.Combine(output, $"{device}-{name}.png"),
                    FullPage = true,
                    Animations = ScreenshotAnimations.Disabled,
                });
                var overflow = await page.EvaluateAsync<int>(
                    "() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
                report.Add($"{device}-{name}: horizontal overflow {overflow}px");
            }
        }

        var anonymous = await OpenAsync(null);
        await anonymous.GotoAsync("/Account/Login");
        await Shot(anonymous, "01-login");
        await anonymous.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await Shot(anonymous, "02-login-errors");
        await anonymous.Context.CloseAsync();

        var member = await OpenAsync(StockroomApp.Member1);
        await Shot(member, "03-member-dashboard");
        await member.GotoAsync("/Inventory");
        await Shot(member, "04-member-inventory");
        await member.GotoAsync("/Inventory?q=zzz");
        await Shot(member, "05-inventory-no-match");
        await member.GotoAsync("/Requests/New?itemId=1");
        await member.GetByLabel("Reason").FillAsync("Prototype enclosure parts");
        await member.GetByRole(AriaRole.Button, new() { Name = "Submit request" }).ClickAsync();
        await Shot(member, "06-request-form-errors");
        await member.GotoAsync("/Requests");
        await Shot(member, "07-member-requests");
        await member.GotoAsync("/Requests/Details/4");
        await Shot(member, "08-member-pending-request");
        await member.GotoAsync("/History");
        await Shot(member, "09-member-history");
        await member.GotoAsync("/Inventory/Issue/1");
        await Shot(member, "10-access-denied");
        await member.GotoAsync("/Requests/Details/2");
        await Shot(member, "11-not-found");
        await member.Context.CloseAsync();

        var manager = await OpenAsync(StockroomApp.Manager);
        await Shot(manager, "12-manager-dashboard");
        await manager.GotoAsync("/Inventory");
        await Shot(manager, "13-manager-inventory");
        await manager.GotoAsync("/Requests");
        await Shot(manager, "14-manager-requests");
        await manager.GotoAsync("/Requests/Details/4");
        await Shot(manager, "15-manager-review");
        await manager.GotoAsync("/Requests/Details/3");
        await Shot(manager, "16-manager-approved");
        await manager.GotoAsync("/Requests/Details/2");
        await Shot(manager, "17-manager-rejected");
        await manager.GotoAsync("/Inventory/Issue/1");
        await manager.GetByLabel("Quantity to issue").FillAsync("0");
        await manager.GetByRole(AriaRole.Button, new() { Name = "Issue stock" }).ClickAsync();
        await Shot(manager, "18-issue-form-errors");
        await manager.GotoAsync("/History");
        await Shot(manager, "19-manager-history");
        await manager.Context.CloseAsync();

        await File.WriteAllLinesAsync(Path.Combine(output, "overflow.txt"), report, ct);
    }
}
