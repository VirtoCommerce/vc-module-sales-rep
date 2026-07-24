using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepMemberResponseGroupParser
{
    string GetResponseGroup(IList<string> includeFields);
}
