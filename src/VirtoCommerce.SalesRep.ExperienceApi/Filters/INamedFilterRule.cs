namespace VirtoCommerce.SalesRep.ExperienceApi.Filters;

public interface INamedFilterRule
{
    string Name { get; set; }

    string LocalizedName { get; set; }
}
