using Xunit;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Dashboard;

/// <summary>Group 6 of the storefront tests: one backend, one seed, one browser, tests run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DashboardCollection : ICollectionFixture<DashboardEnvironment>
{
    public const string Name = "Storefront E2E: dashboard";
}
