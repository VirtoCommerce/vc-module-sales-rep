using System;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Mirrors the platform's OptionalDependencyManager (registered in Platform.Web Startup, which the harness does not
/// boot). Resolving lazily is the point: a slice the test did not add leaves HasValue false, which is how the
/// "task management is not installed" cases are exercised.
/// </summary>
public class TestOptionalDependency<T> : IOptionalDependency<T>
{
    private readonly IServiceProvider _serviceProvider;

    public TestOptionalDependency(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool HasValue => Value != null;

    public T Value => _serviceProvider.GetService<T>();
}
