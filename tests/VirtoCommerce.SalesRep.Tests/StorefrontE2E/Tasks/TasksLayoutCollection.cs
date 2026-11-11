using Xunit;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Tasks;

/// <summary>Group 6 of the storefront tests, the tasks page's layout: one backend, one seed, one browser, tests run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TasksLayoutCollection : ICollectionFixture<TasksLayoutEnvironment>
{
    public const string Name = "Storefront E2E: tasks layout";
}
