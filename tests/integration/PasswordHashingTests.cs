using System.Buffers.Binary;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stockroom.Web;
using Stockroom.Web.Data;

namespace Stockroom.IntegrationTests;

// Release security review R-04: password hashes use PBKDF2-HMAC-SHA512 with PasswordHashing.Iterations, and an
// account whose hash predates the change still signs in and receives an upgraded hash.
public sealed class PasswordHashingTests : IAsyncLifetime
{
    private const int PreviousIterations = 100_000;

    private readonly StockroomFactory factory = new();

    public async ValueTask InitializeAsync() => await factory.SeedAsync();

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task NewHashesUseTheConfiguredIterationCount()
    {
        var hashes = await factory.RunAsync((_, db) => db.Users.Select(u => u.PasswordHash!).ToListAsync());

        Assert.Equal(3, hashes.Count);
        Assert.All(hashes, hash =>
        {
            var (sha512, iterations) = Parameters(hash);
            Assert.True(sha512);
            Assert.Equal(PasswordHashing.Iterations, iterations);
            // OWASP Password Storage Cheat Sheet minimum for PBKDF2-HMAC-SHA512.
            Assert.InRange(iterations, 220_000, int.MaxValue);
        });
    }

    [Fact]
    public async Task LowerIterationHashSignsInAndIsUpgraded()
    {
        // Give a test account the same password hashed with Identity's previous default iteration count.
        var legacy = new PasswordHasher<IdentityUser>(Options.Create(new PasswordHasherOptions { IterationCount = PreviousIterations }));
        await factory.RunAsync(async (_, db) =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == DemoSeeder.Member2Email);
            user.PasswordHash = legacy.HashPassword(user, StockroomFactory.MemberPassword);
            return await db.SaveChangesAsync();
        });
        var before = await AccountAsync();
        Assert.Equal((true, PreviousIterations), Parameters(before.Hash));

        var login = await StockroomFactory.LoginAsync(factory.CreateBrowserClient(), DemoSeeder.Member2Email, StockroomFactory.MemberPassword);
        var after = await AccountAsync();
        var again = await StockroomFactory.LoginAsync(factory.CreateBrowserClient(), DemoSeeder.Member2Email, StockroomFactory.MemberPassword);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location?.OriginalString);
        Assert.Equal((true, PasswordHashing.Iterations), Parameters(after.Hash));
        // Identity rotates the security stamp when it replaces the hash, which ends the account's other sessions.
        Assert.NotEqual(before.Stamp, after.Stamp);
        Assert.Equal("/", again.Headers.Location?.OriginalString);
    }

    private Task<(string Hash, string Stamp)> AccountAsync() =>
        factory.RunAsync(async (_, db) => await db.Users.AsNoTracking()
            .Where(u => u.Email == DemoSeeder.Member2Email)
            .Select(u => new ValueTuple<string, string>(u.PasswordHash!, u.SecurityStamp!))
            .SingleAsync());

    // Identity V3 format: a 0x01 marker, then the PRF (2 = HMAC-SHA512) and the iteration count as big-endian integers.
    private static (bool Sha512, int Iterations) Parameters(string hash)
    {
        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(0x01, bytes[0]);
        return (BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1, 4)) == 2, (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4)));
    }
}
