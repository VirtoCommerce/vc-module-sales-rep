using Xunit;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.SalesReps;

/// <summary>Group 1 of the VC-Shell app tests: one backend, one seed, one browser, tests run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SalesRepsCollection : ICollectionFixture<SalesRepsEnvironment>
{
    public const string Name = "VCShellAppE2E.SalesReps";
}
