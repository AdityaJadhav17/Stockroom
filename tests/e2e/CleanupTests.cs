using Microsoft.Playwright;

namespace Stockroom.E2ETests;

// Fixture regression: cleanup must stop the application and delete its directory even when a test has
// already closed a browser context.
public sealed class CleanupTests
{
    private sealed class Harness : BrowserTest
    {
        public StockroomApp Application => App;

        public Task<IPage> LogInAs(string email) => LogInAsync(email);
    }

    [Fact]
    public async Task CleanupStopsTheApplicationAfterATestClosesItsBrowserContext()
    {
        var harness = new Harness();
        await harness.InitializeAsync();
        var page = await harness.LogInAs(StockroomApp.Member1);
        var baseUrl = harness.Application.BaseUrl;
        var directory = harness.Application.DirectoryPath;

        await page.Context.CloseAsync();
        await harness.DisposeAsync();

        Assert.False(Directory.Exists(directory));
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            http.GetAsync(new Uri(baseUrl, "/Account/Login"), TestContext.Current.CancellationToken));
    }
}
