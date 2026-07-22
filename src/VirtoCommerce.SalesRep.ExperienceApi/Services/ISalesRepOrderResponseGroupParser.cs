using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepOrderResponseGroupParser
{
    string GetResponseGroup(IList<string> includeFields);
}
