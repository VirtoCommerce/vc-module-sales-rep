using Xunit;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Calendar;

/// <summary>Group 2 of the storefront tests: one backend, one seed, one browser, tests run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CalendarCollection : ICollectionFixture<CalendarEnvironment>
{
    public const string Name = "Storefront E2E: calendar";
}
