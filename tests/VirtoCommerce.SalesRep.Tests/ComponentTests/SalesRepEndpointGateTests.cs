using System;
using System.Linq;
using FluentAssertions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.SalesRep.ExperienceApi.Queries;
using VirtoCommerce.Xapi.Core.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

// The gate lives in three builder bases, so it protects an endpoint only if that endpoint derives from one.
[Trait("Category", "Unit")]
public class SalesRepEndpointGateTests
{
    private static readonly Type[] _gateRoots =
    [
        typeof(SalesRepQueryBuilder<,,>),
        typeof(SalesRepSearchQueryBuilder<,,,>),
        typeof(SalesRepCommandBuilder<,,,>),
    ];

    [Fact]
    public void EverySchemaBuilder_DerivesFromAGatedBase()
    {
        // Scanned rather than resolved from DI: this also catches a builder that exists but was never
        // registered, and needs no harness to read a static property.
        var builders = typeof(XapiAssemblyMarker).Assembly.GetTypes()
            .Where(x => !x.IsAbstract && typeof(ISchemaBuilder).IsAssignableFrom(x))
            .ToList();

        builders.Should().NotBeEmpty("the module exposes its endpoints as schema builders");

        var ungated = builders
            .Where(x => !IsGated(x))
            .Select(x => x.Name)
            .ToList();

        ungated.Should().BeEmpty(
            "every sales-rep endpoint must inherit the account-state and authentication gate from " +
            "SalesRepQueryBuilder, SalesRepSearchQueryBuilder or SalesRepCommandBuilder");
    }

    private static bool IsGated(Type type)
    {
        return type.GetTypeInheritanceChain()
            .Any(x => x.IsGenericType && _gateRoots.Contains(x.GetGenericTypeDefinition()));
    }
}
