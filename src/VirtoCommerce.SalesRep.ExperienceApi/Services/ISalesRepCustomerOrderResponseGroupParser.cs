using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepCustomerOrderResponseGroupParser
{
    string GetResponseGroup(IList<string> includeFields);
}
