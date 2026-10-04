using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stockroom.Web.Data;

namespace Stockroom.IntegrationTests;

// Hosts the web application against a seeded SQLite file that belongs to one test.
public sealed partial class StockroomFactory : WebApplicationFactory<Program>
{
    public const string MemberPassword = "Test-Member-Pass-1";
    public const string ManagerPassword = "Test-Manager-Pass-1";

    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"stockroom-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Stockroom"] = $"Data Source={databasePath}",
            ["Seed:MemberPassword"] = MemberPassword,
            ["Seed:ManagerPassword"] = ManagerPassword,
        }));

    public async Task SeedAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await DemoSeeder.SeedAsync(scope.ServiceProvider);
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
        File.Delete(databasePath);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenPattern();
}
