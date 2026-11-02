using Xunit;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Tasks;

/// <summary>Group 4 of the storefront tests: one backend, one seed, one browser, tests run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TasksCollection : ICollectionFixture<TasksEnvironment>
{
    public const string Name = "Storefront E2E: tasks";
}
