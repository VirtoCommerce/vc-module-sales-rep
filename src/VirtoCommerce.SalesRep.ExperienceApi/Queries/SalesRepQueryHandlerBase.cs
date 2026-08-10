using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepQueryHandlerBase
{
    protected SalesRepQueryHandlerBase(ISalesRepOrganizationAccessService organizationAccessService)
    {
        OrganizationAccessService = organizationAccessService;
    }

    protected ISalesRepOrganizationAccessService OrganizationAccessService { get; }
}
