using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Stockroom.E2ETests;

// Records the portfolio walkthrough against a freshly seeded fixture database: README screenshots in
// docs/images and a video in artifacts/demo (ignored by Git). It is explicit, so normal runs and CI skip it.
// Run: dotnet test --project tests/e2e/Stockroom.E2ETests.csproj -c Release --no-build -- --explicit only
//      --filter-class Stockroom.E2ETests.DemoRecording
public sealed class DemoRecording
{
    private const string Filament = "PLA filament, 1.75 mm";
    private const string Resin = "Standard photopolymer resin, 1 L";

    [Fact(Explicit = true)]
    public async Task RecordWalkthrough()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = StockroomApp.RepositoryRoot();
        var images = Path.Combine(root, "docs", "images");
        var videos = Path.Combine(root, "artifacts", "demo");
        Directory.CreateDirectory(images);
        Directory.CreateDirectory(videos);

        await using var app = await StockroomApp.StartAsync(ct);
        using var playwright = await Playwright.CreateAsync();
        // A short delay between actions keeps the video readable; assertions still wait on page state.
        await using var browser = await playwright.Chromium.LaunchAsync(new() { SlowMo = 250 });
        var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 1280, Height = 800 },
            RecordVideoDir = videos,
            RecordVideoSize = new() { Width = 1280, Height = 800 },
        });
        var page = await context.NewPageAsync();
        var baseUrl = app.BaseUrl;
        string Url(string path) => new Uri(baseUrl, path).ToString();
        Task Shot(string name) => page.ScreenshotAsync(new() { Path = Path.Combine(images, name), FullPage = true });

        async Task LogInAsync(string email)
        {
            await page.GotoAsync(Url("/Account/Login"));
            await page.GetByLabel("Email").FillAsync(email);
            await page.GetByLabel("Password").FillAsync(app.PasswordFor(email));
            await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard" })).ToBeVisibleAsync();
        }

        async Task LogOutAsync()
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Log out" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Log in" })).ToBeVisibleAsync();
        }

        async Task<string> RequestAsync(string item, string quantity, string reason, string? screenshot = null)
        {
            await page.GotoAsync(Url("/Inventory"));
            await page.GetByRole(AriaRole.Link, new() { Name = $"Request {item}" }).ClickAsync();
            await page.GetByLabel("Quantity").FillAsync(quantity);
            await page.GetByLabel("Reason").FillAsync(reason);
            if (screenshot is not null)
            {
                await Shot(screenshot);
            }
            await page.GetByRole(AriaRole.Button, new() { Name = "Submit request" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("submitted.");
            return new Uri(page.Url).AbsolutePath;
        }

        // Member: low-stock dashboard, then two purchase requests.
        await LogInAsync(StockroomApp.Member1);
        await Shot("01-member-dashboard.png");
        var filamentRequest = await RequestAsync(Filament, "5", "Materials for the robotics workshop", "02-request-form.png");
        var resinRequest = await RequestAsync(Resin, "6", "Extra resin for the open house");
        await LogOutAsync();

        // Manager: approve and receive filament, reject resin.
        await LogInAsync(StockroomApp.Manager);
        await Shot("03-manager-dashboard.png");
        await page.GotoAsync(Url(filamentRequest));
        await Shot("04-manager-review.png");
        await page.GetByRole(AriaRole.Button, new() { Name = "Approve request" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("approved.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Record receipt of 5 spools" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Added 5 spools to stock.");
        await Shot("05-receipt-recorded.png");

        await page.GotoAsync(Url(resinRequest));
        await page.GetByLabel("Rejection reason").FillAsync("Resin stock covers the open house. Request again in October.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Reject request" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("rejected.");

        // Withdrawal: issue two spools, then refuse six of the remaining five.
        await page.GotoAsync(Url("/Inventory"));
        await page.GetByRole(AriaRole.Link, new() { Name = $"Issue {Filament}" }).ClickAsync();
        await page.GetByLabel("Quantity to issue").FillAsync("2");
        await page.GetByLabel("Reason").FillAsync("Robotics workshop prints");
        await page.GetByRole(AriaRole.Button, new() { Name = "Issue stock" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync($"Issued 2 spools of {Filament}. 5 spools remain.");
        await page.GetByRole(AriaRole.Link, new() { Name = $"Issue {Filament}" }).ClickAsync();
        await page.GetByLabel("Quantity to issue").FillAsync("6");
        await page.GetByLabel("Reason").FillAsync("Open house display");
        await page.GetByRole(AriaRole.Button, new() { Name = "Issue stock" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Alert)).ToHaveTextAsync($"Only 5 spools of {Filament} in stock. Nothing was changed.");
        await Shot("06-issue-refused.png");

        await page.GotoAsync(Url("/History"));
        await Shot("07-manager-history.png");

        // Restart persistence: the new process listens on a new port; the data must still be there.
        await app.RestartAsync(ct);
        baseUrl = app.BaseUrl;
        await LogInAsync(StockroomApp.Manager);
        await page.GotoAsync(Url("/Inventory?q=filament"));
        await Expect(page.GetByRole(AriaRole.Row).Filter(new() { HasText = Filament }).GetByRole(AriaRole.Cell).Nth(2)).ToHaveTextAsync("5");
        await page.GotoAsync(Url("/History"));
        await Expect(page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Robotics workshop prints" })).ToContainTextAsync("-2 spools");
        await LogOutAsync();

        // Member: the rejection and its reason.
        await LogInAsync(StockroomApp.Member1);
        await page.GotoAsync(Url(resinRequest));
        await Expect(page.GetByRole(AriaRole.Definition).Filter(new() { HasText = "Resin stock covers the open house." })).ToBeVisibleAsync();
        await Shot("08-member-rejection.png");

        var video = page.Video!;
        await context.CloseAsync();
        await video.SaveAsAsync(Path.Combine(videos, "stockroom-walkthrough.webm"));
        await video.DeleteAsync();
    }
}
