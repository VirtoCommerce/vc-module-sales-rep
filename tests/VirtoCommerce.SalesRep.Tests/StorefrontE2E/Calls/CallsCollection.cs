using Xunit;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Calls;

/// <summary>Group 3 of the storefront tests: one backend, one seed, one browser, tests run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CallsCollection : ICollectionFixture<CallsEnvironment>
{
    public const string Name = "Storefront E2E: calls";
}
