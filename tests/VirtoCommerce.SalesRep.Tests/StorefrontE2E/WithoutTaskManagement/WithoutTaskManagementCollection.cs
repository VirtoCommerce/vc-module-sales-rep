using Xunit;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.WithoutTaskManagement;

/// <summary>Group 5 of the storefront tests: one backend, one seed, one browser, tests run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WithoutTaskManagementCollection : ICollectionFixture<WithoutTaskManagementEnvironment>
{
    public const string Name = "Storefront E2E: without task management";
}
