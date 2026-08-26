using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCustomerInsightsContext
{
    private readonly ConcurrentDictionary<string, Lazy<Task<object>>> _slices = new();

    public IList<string> OrganizationIds { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    // One fetch per (collection, sort, take): aliased selections and the dataAsOf resolver share the same read.
    public virtual async Task<T> GetOrAddSliceAsync<T>(string key, Func<Task<T>> factory) where T : class
    {
        var slice = _slices.GetOrAdd(key, _ => new Lazy<Task<object>>(async () => await factory(), LazyThreadSafetyMode.ExecutionAndPublication));
        return (T)await slice.Value;
    }
}
