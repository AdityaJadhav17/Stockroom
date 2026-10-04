using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Models;
using Stockroom.Web.Services;

namespace Stockroom.IntegrationTests;

// Hosts the web application against a seeded SQLite file that belongs to one test.
// busyTimeoutSeconds shortens the provider's lock retries for lock-contention tests (default 30 seconds).
public sealed partial class StockroomFactory(int? busyTimeoutSeconds = null) : WebApplicationFactory<Program>
{
    public const string MemberPassword = "Test-Member-Pass-1";
    public const string ManagerPassword = "Test-Manager-Pass-1";

    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"stockroom-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Stockroom"] = busyTimeoutSeconds is { } seconds
                ? $"Data Source={DatabasePath};Default Timeout={seconds}"
                : $"Data Source={DatabasePath}",
            ["Seed:MemberPassword"] = MemberPassword,
            ["Seed:ManagerPassword"] = ManagerPassword,
        }));

    public async Task SeedAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await DemoSeeder.SeedAsync(scope.ServiceProvider);
    }

    // Each call uses its own scope, so concurrent calls use separate contexts and connections to one file.
    public async Task<T> RunAsync<T>(Func<PurchaseService, AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(
            scope.ServiceProvider.GetRequiredService<PurchaseService>(),
            scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task<string> UserIdAsync(string email) =>
        RunAsync((_, db) => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

    public async Task AddRoleAsync(string email, string role)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var result = await users.AddToRoleAsync((await users.FindByEmailAsync(email))!, role);
        Assert.True(result.Succeeded);
    }

    // Request fields, item quantity, and history counts; equal snapshots mean an operation changed nothing.
    public Task<string> SnapshotAsync(int requestId) =>
        RunAsync(async (_, db) =>
        {
            var r = await db.PurchaseRequests.AsNoTracking().SingleAsync(x => x.Id == requestId);
            var quantity = await db.InventoryItems.Where(i => i.Id == r.ItemId).Select(i => i.Quantity).SingleAsync();
            var events = await db.RequestEvents.CountAsync();
            var movements = await db.StockMovements.CountAsync();
            return $"{r.Status}|{r.ReviewerId}|{r.RejectionReason}|{r.ReceiverId}|{quantity}|{events}|{movements}";
        });

    public Task<InventoryItem> ItemAsync(string sku) =>
        RunAsync((_, db) => db.InventoryItems.AsNoTracking().SingleAsync(i => i.Sku == sku));

    public async Task<HttpClient> LoggedInClientAsync(string email)
    {
        var client = CreateBrowserClient();
        await LoginAsync(client, email, email == DemoSeeder.ManagerEmail ? ManagerPassword : MemberPassword);
        return client;
    }

    // The layout's logout form gives every signed-in page an antiforgery token.
    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string path, Dictionary<string, string>? fields = null)
    {
        var form = new Dictionary<string, string>(fields ?? [])
        {
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync(client, "/"),
        };
        return await client.PostAsync(path, new FormUrlEncodedContent(form));
    }

    public HttpClient CreateBrowserClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        return AntiforgeryTokenPattern().Match(html).Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = token,
        }));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(DatabasePath);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenPattern();
}
