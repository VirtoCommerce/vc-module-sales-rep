using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerActivitySummaryQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerActivitySummaryQuery, SalesRepCustomerActivitySummary>
{
    private readonly ISalesRepCustomerActivityService _customerActivityService;
    private readonly IMemberService _memberService;
    private readonly ISalesRepProductResolver _productResolver;

    public SalesRepCustomerActivitySummaryQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCustomerActivityService customerActivityService,
        IMemberService memberService,
        ISalesRepProductResolver productResolver)
        : base(organizationAccessService)
    {
        _customerActivityService = customerActivityService;
        _memberService = memberService;
        _productResolver = productResolver;
    }

    public virtual async Task<SalesRepCustomerActivitySummary> Handle(SalesRepCustomerActivitySummaryQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.OrganizationId))
        {
            return null;
        }

        if (!await OrganizationAccessService.ServesOrganizationAsync(request.UserId, request.OrganizationId))
        {
            return null;
        }

        var criteria = AbstractTypeFactory<SalesRepCustomerActivityCriteria>.TryCreateInstance();
        criteria.OrganizationId = request.OrganizationId;
        criteria.StoreId = request.StoreId;
        criteria.From = request.Period?.From;
        criteria.To = request.Period?.To;

        var result = await _customerActivityService.GetSummaryAsync(criteria);

        var organization = (await _memberService.GetByIdsAsync([request.OrganizationId], null, [nameof(Organization)])).FirstOrDefault();
        result.CreatedOn = organization?.CreatedDate;

        await ResolveLastViewedProductAsync(result, request);

        return result;
    }

    protected virtual async Task ResolveLastViewedProductAsync(SalesRepCustomerActivitySummary result, SalesRepCustomerActivitySummaryQuery request)
    {
        var code = result.LastViewedProduct?.Code;
        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        var productsByCode = await _productResolver.ResolveByCodesAsync([code]);
        if (!productsByCode.TryGetValue(code, out var product))
        {
            return;
        }

        result.LastViewedProduct.ProductId = product.ProductId;
        result.LastViewedProduct.ImageUrl = product.ImageUrl;

        if (!string.IsNullOrEmpty(product.Name))
        {
            result.LastViewedProduct.Name = product.Name;
        }
    }
}
