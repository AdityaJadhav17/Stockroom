namespace Stockroom.IntegrationTests;

// Real-time login-window tests must not compete with password hashing in the other test collections.
// The remaining integration test collections retain their normal parallel execution.
[CollectionDefinition("Security timing", DisableParallelization = true)]
public sealed class SecurityTimingCollection
{
}
