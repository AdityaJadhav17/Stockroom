using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stockroom.Web.Data;
using Stockroom.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Read the connection string when the context is created so test hosts can override it.
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseSqlite(services.GetRequiredService<IConfiguration>().GetConnectionString("Stockroom")));

// No token providers or account pages: public registration and password recovery are out of scope.
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
        options.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(PurchaseRules.RequesterPolicy, policy => policy.RequireAssertion(c => PurchaseRules.CanRequestPurchases(c.User)));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
});

var app = builder.Build();

// Development-only demo data: `seed` preserves existing records; `seed --reset` deletes the local database first.
if (args.Contains("seed"))
{
    return await DemoSeeder.RunCommandAsync(app.Services, app.Environment, args.Contains("--reset"), Console.Out, Console.Error);
}

app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
return 0;
