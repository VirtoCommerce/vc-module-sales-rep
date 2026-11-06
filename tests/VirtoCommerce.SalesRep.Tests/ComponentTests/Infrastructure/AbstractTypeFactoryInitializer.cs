using System.Runtime.CompilerServices;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Simulates a downstream module extending the layout contract with derived types, at the ROOT (Layout) and a nested
/// collection element (LayoutBlock), so persistence round-trips prove both. Registered once, at assembly load: the type
/// factory is process-wide and not thread-safe, and xunit runs test classes in parallel. (The platform's and the
/// hosted modules' own registrations are <see cref="Tests.Infrastructure.PlatformStatics"/>'.)
/// </summary>
internal static class AbstractTypeFactoryInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        AbstractTypeFactory<Layout>.OverrideType<Layout, TestExtendedLayout>();
        AbstractTypeFactory<LayoutBlock>.OverrideType<LayoutBlock, TestExtendedLayoutBlock>();
    }
}
