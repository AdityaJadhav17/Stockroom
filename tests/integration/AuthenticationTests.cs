using System.Net;
using Stockroom.Web.Data;
using Stockroom.Web.Pages.Account;

namespace Stockroom.IntegrationTests;

// T-01: valid and invalid login, logout, and unauthenticated access (US-01).
public sealed class AuthenticationTests : IAsyncLifetime
{
    private readonly StockroomFactory factory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.SeedAsync();

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task UnauthenticatedDashboardRequestRedirectsToLogin()
    {
        var client = factory.CreateBrowserClient();

        var response = await client.GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Theory]
    [InlineData(DemoSeeder.Member1Email, StockroomFactory.MemberPassword, "Member")]
    [InlineData(DemoSeeder.ManagerEmail, StockroomFactory.ManagerPassword, "Manager")]
    public async Task ValidLoginOpensDashboardWithRole(string email, string password, string role)
    {
        var client = factory.CreateBrowserClient();

        var login = await StockroomFactory.LoginAsync(client, email, password);
        var dashboard = await client.GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        var html = await dashboard.Content.ReadAsStringAsync(Ct);
        Assert.Contains(email, html);
        Assert.Contains($"<span class=\"account-role\">{role}</span>", html);
    }

    [Theory]
    [InlineData(DemoSeeder.Member1Email, "Wrong-Password-1")]
    [InlineData("nobody@stockroom.test", StockroomFactory.MemberPassword)]
    public async Task InvalidLoginShowsNeutralErrorAndCreatesNoSession(string email, string password)
    {
        var client = factory.CreateBrowserClient();

        var login = await StockroomFactory.LoginAsync(client, email, password);
        var dashboard = await client.GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(LoginModel.InvalidLoginMessage, await login.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
    }

    [Fact]
    public async Task LoginIgnoresExternalReturnUrl()
    {
        var client = factory.CreateBrowserClient();
        var token = await StockroomFactory.GetAntiforgeryTokenAsync(client, "/Account/Login");

        var login = await client.PostAsync("/Account/Login?returnUrl=https%3A%2F%2Fexample.com%2F",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = DemoSeeder.Member1Email,
                ["Input.Password"] = StockroomFactory.MemberPassword,
                ["__RequestVerificationToken"] = token,
            }), Ct);

        Assert.Equal("/", login.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task LogoutEndsSession()
    {
        var client = factory.CreateBrowserClient();
        await StockroomFactory.LoginAsync(client, DemoSeeder.Member1Email, StockroomFactory.MemberPassword);
        var token = await StockroomFactory.GetAntiforgeryTokenAsync(client, "/");

        var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token }), Ct);
        var dashboard = await client.GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
        Assert.Equal("/Account/Login", dashboard.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task LogoutWithoutAntiforgeryTokenIsRejected()
    {
        var client = factory.CreateBrowserClient();
        await StockroomFactory.LoginAsync(client, DemoSeeder.Member1Email, StockroomFactory.MemberPassword);

        var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent([]), Ct);
        var dashboard = await client.GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
    }

    [Theory]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ForgotPassword")]
    public async Task RegistrationAndPasswordRecoveryAreUnavailable(string path)
    {
        var client = factory.CreateBrowserClient();

        var response = await client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
