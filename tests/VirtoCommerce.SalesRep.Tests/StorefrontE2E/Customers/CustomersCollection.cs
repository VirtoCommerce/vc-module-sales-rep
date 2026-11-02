using Xunit;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Customers;

/// <summary>Group 1 of the storefront tests: one backend, one seed, one browser, tests run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CustomersCollection : ICollectionFixture<CustomersEnvironment>
{
    public const string Name = "Storefront E2E: my customers";
}
