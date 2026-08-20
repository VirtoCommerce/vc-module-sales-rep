using System.Collections.Generic;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepCartStatisticsResponseGroupParser
{
    CartStatisticsResponseGroup GetResponseGroup(IList<string> includeFields);
}
