using System.Reflection;

namespace Stockroom.IntegrationTests;

// Theory arguments become part of test names, and CI uploads test names in TRX reports (release security review R-01).
// Theories therefore take a case key and resolve any password inside the test.
public sealed class TheoryDataTests
{
    [Fact]
    public void NoTheoryTakesAPasswordArgument()
    {
        string[] fixturePasswords = [StockroomFactory.MemberPassword, StockroomFactory.ManagerPassword];
        var offenders = typeof(TheoryDataTests).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Where(method => method.GetCustomAttributes<InlineDataAttribute>().Any())
            .Where(method =>
                method.GetParameters().Any(p => p.Name?.Contains("password", StringComparison.OrdinalIgnoreCase) == true)
                || method.GetCustomAttributes<InlineDataAttribute>().Any(data =>
                    data.Data.OfType<string>().Any(value => fixturePasswords.Any(password => value.Contains(password, StringComparison.Ordinal)))))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToList();

        Assert.Empty(offenders);
    }
}
