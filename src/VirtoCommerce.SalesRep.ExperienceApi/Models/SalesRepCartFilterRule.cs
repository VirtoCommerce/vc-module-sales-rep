using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCartFilterRule : INamedFilterRule
{
    public string Name { get; set; }

    public string LocalizedName { get; set; }

    public IList<string> Names { get; set; } = [];

    public IList<string> Types { get; set; } = [];

    public IList<string> ExcludeTypes { get; set; } = [];

    public IList<string> Statuses { get; set; } = [];

    public static SalesRepCartFilterRule Create(
        string name,
        string localizedName,
        IList<string> types = null,
        IList<string> statuses = null,
        IList<string> excludeTypes = null,
        IList<string> names = null)
    {
        var result = AbstractTypeFactory<SalesRepCartFilterRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.Names = names ?? [];
        result.Types = types ?? [];
        result.Statuses = statuses ?? [];
        result.ExcludeTypes = excludeTypes ?? [];
        return result;
    }
}
