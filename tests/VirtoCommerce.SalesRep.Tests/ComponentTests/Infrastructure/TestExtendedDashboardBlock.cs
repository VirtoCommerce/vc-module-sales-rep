using VirtoCommerce.SalesRep.Core.Models.Dashboard;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Stands in for a downstream module's derived block type: extends the platform <see cref="DashboardBlock"/> with
/// a field the base contract knows nothing about. Registered via AbstractTypeFactory in
/// <see cref="AbstractTypeFactoryInitializer"/> so the layout round-trip can prove the derived type (and its extra
/// field) survives persistence.
/// </summary>
public class TestExtendedDashboardBlock : DashboardBlock
{
    public string ColorScheme { get; set; }
}
