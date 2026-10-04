using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Stockroom.E2ETests;

// At phone width, every main page must fit the viewport: wide tables scroll inside their own frame instead of
// the whole page. The skip link must stay the first keyboard stop, and the navigation marks the current section.
public sealed class ResponsiveLayoutTests : BrowserTest
{
    private static readonly string[] MemberPages =
        ["/", "/Inventory", "/Requests", "/Requests/New", "/Requests/Details/4", "/History"];

    private static readonly string[] ManagerPages =
        ["/", "/Inventory", "/Requests", "/Requests/Details/4", "/Requests/Details/3", "/Inventory/Issue/1", "/History"];

    [Fact]
    public async Task PagesFitAPhoneWidthWithoutSidewaysScrolling()
    {
        var member = await LogInAsync(StockroomApp.Member1);
        var manager = await LogInAsync(StockroomApp.Manager);
        // The redesign must work under the strict Content-Security-Policy; a blocked style or script logs an error.
        var consoleErrors = new List<string>();

        foreach (var (page, paths) in new[] { (member, MemberPages), (manager, ManagerPages) })
        {
            page.Console += (_, message) =>
            {
                if (message.Type == "error")
                {
                    consoleErrors.Add($"{page.Url}: {message.Text}");
                }
            };
            await page.SetViewportSizeAsync(390, 844);
            foreach (var path in paths)
            {
                await page.GotoAsync(path);
                var overflow = await page.EvaluateAsync<int>(
                    "() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
                Assert.True(overflow <= 0, $"{path} scrolls sideways by {overflow}px at 390px wide.");
            }
        }
        Assert.Empty(consoleErrors);
    }

    // Stacked cards clip content that does not fit, so the page check above cannot see it; compare each cell's
    // content width with its box at the narrowest common phone width.
    [Fact]
    public async Task StackedCardsShowEveryValueInFullAt320Pixels()
    {
        var manager = await LogInAsync(StockroomApp.Manager);
        await manager.SetViewportSizeAsync(320, 640);

        foreach (var path in new[] { "/", "/Inventory", "/Requests" })
        {
            await manager.GotoAsync(path);
            var clipped = await manager.EvaluateAsync<string[]>(
                "() => [...document.querySelectorAll('.stack-sm td')]"
                + ".filter(td => td.scrollWidth > td.clientWidth).map(td => td.textContent.trim())");
            Assert.True(clipped.Length == 0, $"{path} clips: {string.Join(" | ", clipped)}");
        }
    }

    // The sticky header must never cover the focused element or an anchor target (WCAG 2.4.11), at a desktop width
    // where it is sticky and at the narrowest width before it stops sticking.
    [Fact]
    public async Task StickyHeaderNeverCoversFocusOrAnchorTargets()
    {
        var manager = await LogInAsync(StockroomApp.Manager);
        const string Obscured = """
            () => {
                const bar = document.querySelector('.site-header');
                const focused = document.activeElement;
                if (bar.contains(focused) || focused.classList.contains('skip-link')) return null;
                const header = bar.getBoundingClientRect();
                const target = focused.getBoundingClientRect();
                // An element that fits below the header must be fully clear of it; a taller one (a table frame)
                // must still show part of itself, which is what WCAG 2.4.11 requires.
                const fits = target.height <= window.innerHeight - header.bottom;
                const covered = fits ? target.top < header.bottom - 1 : target.bottom <= header.bottom;
                return covered ? focused.outerHTML.slice(0, 80) : null;
            }
            """;

        foreach (var (width, height) in new[] { (1280, 600), (900, 600) })
        {
            await manager.SetViewportSizeAsync(width, height);
            await manager.GotoAsync("/History");
            for (var i = 0; i < 30; i++)
            {
                await manager.Keyboard.PressAsync("Tab");
                Assert.Null(await manager.EvaluateAsync<string?>(Obscured));
            }
            await manager.EvaluateAsync("() => window.scrollTo(0, document.body.scrollHeight)");
            for (var i = 0; i < 30; i++)
            {
                await manager.Keyboard.PressAsync("Shift+Tab");
                Assert.Null(await manager.EvaluateAsync<string?>(Obscured));
            }

            await manager.GotoAsync("/History#movements-heading");
            var gap = await manager.EvaluateAsync<double>(
                "() => document.getElementById('movements-heading').getBoundingClientRect().top"
                + " - document.querySelector('.site-header').getBoundingClientRect().bottom");
            Assert.True(gap >= 0, $"At {width}px the header covers the anchored heading by {-gap}px.");
        }
    }

    [Fact]
    public async Task SkipLinkIsTheFirstKeyboardStopAndNavigationMarksTheSection()
    {
        var member = await LogInAsync(StockroomApp.Member1);
        await member.GotoAsync("/Inventory");

        await member.Keyboard.PressAsync("Tab");
        var skip = member.GetByRole(AriaRole.Link, new() { Name = "Skip to main content" });
        await Expect(skip).ToBeFocusedAsync();
        await Expect(skip).ToBeInViewportAsync();

        var nav = member.GetByRole(AriaRole.Navigation, new() { Name = "Main" });
        await Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Inventory" })).ToHaveAttributeAsync("aria-current", "page");
        await Expect(nav.Locator("[aria-current]")).ToHaveCountAsync(1);
    }
}
