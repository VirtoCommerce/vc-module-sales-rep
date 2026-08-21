using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.SalesRep.ExperienceApi.Queries;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.Xapi.Core.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The module's gate — signed in, account still usable, then membership scoping — lives in three builder bases,
/// so it protects an endpoint only if that endpoint derives from one of them. SalesRepAccountStateGraphQlTests
/// proves the gate works; this one proves nothing escapes it, including endpoints added later.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepEndpointGateTests
{
    private static readonly string[] _gateRoots =
    [
        typeof(SalesRepQueryBuilder<,,>).Name,
        typeof(SalesRepSearchQueryBuilder<,,,>).Name,
        typeof(SalesRepCommandBuilder<,,,>).Name,
    ];

    [Fact]
    public void EverySchemaBuilder_DerivesFromAGatedBase()
    {
        using var ctx = SalesRepTestContext.Create();

        var builders = ctx.GetRequiredService<IEnumerable<ISchemaBuilder>>()
            .Where(x => x.GetType().Assembly == typeof(XapiAssemblyMarker).Assembly)
            .ToList();

        builders.Should().NotBeEmpty("the module registers its endpoints as schema builders");

        var ungated = builders
            .Where(x => !IsGated(x.GetType()))
            .Select(x => x.GetType().Name)
            .ToList();

        ungated.Should().BeEmpty(
            "every sales-rep endpoint must inherit the account-state and authentication gate from " +
            "SalesRepQueryBuilder, SalesRepSearchQueryBuilder or SalesRepCommandBuilder");
    }

    private static bool IsGated(System.Type type)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (_gateRoots.Contains(current.Name))
            {
                return true;
            }
        }

        return false;
    }
}
