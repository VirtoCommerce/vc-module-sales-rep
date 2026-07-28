using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services.Statistics;

public interface ICustomerCartStatisticsService
{
    Task<CustomerCartStatisticsPeriod> GetStatisticsAsync(CustomerCartStatisticsCriteria criteria);
}
