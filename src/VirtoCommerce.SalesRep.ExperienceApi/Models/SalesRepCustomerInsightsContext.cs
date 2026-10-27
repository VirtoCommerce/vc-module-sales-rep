using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCustomerInsightsContext
{
    private readonly ConcurrentDictionary<string, object> _slices = new();

    public IList<string> OrganizationIds { get; set; }

    public string StoreId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    // Turned off by the collection resolvers when a read fails — an empty list cannot say why it is empty.
    public bool IsAnalyticsAvailable { get; set; } = true;

    // One fetch per (collection, sort, take): aliased selections and isAnalyticsAvailable share the same read.
    public virtual Task<T> GetOrAddSliceAsync<T>(string key, Func<Task<T>> factory) where T : class
    {
        return ((Lazy<Task<T>>)_slices.GetOrAdd(key, _ => new Lazy<Task<T>>(factory))).Value;
    }
}
