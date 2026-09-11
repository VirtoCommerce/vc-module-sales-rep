using System;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Mirrors the platform's OptionalDependencyManager (Platform.Modules, not referenceable here): the dependency is
/// "present" exactly when <typeparamref name="T"/> is registered — so the default harness models the analytics
/// module being absent, and a test override registering <c>IAnalyticsService</c> models it installed.
/// </summary>
internal sealed class TestOptionalDependency<T> : IOptionalDependency<T>
{
    private readonly IServiceProvider _serviceProvider;

    public TestOptionalDependency(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool HasValue => Value != null;

    public T Value => _serviceProvider.GetService<T>();
}
