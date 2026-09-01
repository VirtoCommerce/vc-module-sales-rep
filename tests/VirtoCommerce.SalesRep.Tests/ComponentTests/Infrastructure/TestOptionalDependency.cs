using System;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

// Mirrors the platform's OptionalDependencyManager, which is registered in Platform.Web Startup - not booted
// here. Resolving lazily is the point: a slice the test omits leaves HasValue false.
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
