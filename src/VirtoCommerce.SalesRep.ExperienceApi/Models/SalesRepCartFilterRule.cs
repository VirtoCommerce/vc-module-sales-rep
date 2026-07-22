using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCartFilterRule : INamedFilterRule
{
    public string Name { get; set; }

    public string LocalizedName { get; set; }

    public IList<string> Types { get; set; } = [];

    public IList<string> ExcludeTypes { get; set; } = [];

    public IList<string> Statuses { get; set; } = [];

    public bool OnlyNonEmpty { get; set; }

    public static SalesRepCartFilterRule Create(
        string name,
        string localizedName,
        IList<string> types = null,
        IList<string> statuses = null,
        IList<string> excludeTypes = null,
        bool onlyNonEmpty = false)
    {
        var result = AbstractTypeFactory<SalesRepCartFilterRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.Types = types ?? [];
        result.Statuses = statuses ?? [];
        result.ExcludeTypes = excludeTypes ?? [];
        result.OnlyNonEmpty = onlyNonEmpty;
        return result;
    }
}
