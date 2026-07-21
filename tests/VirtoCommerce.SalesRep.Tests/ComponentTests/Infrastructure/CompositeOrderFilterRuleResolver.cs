using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// A realistic "project override" of the REAL default <see cref="SalesRepOrderFilterRuleResolver"/> — it keeps every
/// configured 1:1 status rule (from <c>base.GetRulesAsync</c>) and adds one composite (1:many) rule,
/// "Inactive" → { Cancelled, Failed }. All resolution/apply logic stays the real base class, so tests that build the
/// harness with this override exercise the documented composite-grouping seam end to end (not a from-scratch stub).
/// </summary>
internal sealed class CompositeOrderFilterRuleResolver : SalesRepOrderFilterRuleResolver
{
    /// <summary>The composite rule's stable name (sent back as the orders/statistics <c>filter</c> argument).</summary>
    public const string CompositeName = "Inactive";

    /// <summary>The composite rule's localized label.</summary>
    public const string CompositeLabel = "Not active";

    public CompositeOrderFilterRuleResolver(ILocalizableSettingService localizableSettingService)
        : base(localizableSettingService)
    {
    }

    public override async Task<IList<SalesRepOrderFilterRule>> GetRulesAsync(string storeId, string cultureName)
    {
        // base returns a fresh List<>, so appending the composite is safe.
        var rules = await base.GetRulesAsync(storeId, cultureName);
        rules.Add(SalesRepOrderFilterRule.Create(CompositeName, CompositeLabel, "Cancelled", "Failed"));
        return rules;
    }
}

/// <summary>Harness override hooks for <see cref="SalesRepTestContext.Create"/>.</summary>
internal static class OrderFilterRuleOverride
{
    /// <summary>
    /// Replaces the default (real, 1:1) order-status resolver with <see cref="CompositeOrderFilterRuleResolver"/> so a
    /// test can exercise composite (1:many) status resolution. Last registration wins, so this shadows the default one
    /// registered by <c>AddSalesRepGraphQl</c>.
    /// </summary>
    public static void WithCompositeInactiveStatus(IServiceCollection services)
        => services.AddSingleton<ISalesRepOrderFilterRuleResolver, CompositeOrderFilterRuleResolver>();
}
