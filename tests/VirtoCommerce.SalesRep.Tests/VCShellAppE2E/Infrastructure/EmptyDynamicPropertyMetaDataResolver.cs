using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.DynamicProperties;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>
/// The harness defines no dynamic properties. The platform's JSON contract still asks a metadata resolver about
/// every member it serializes (through the process-wide <see cref="DynamicPropertyMetadata"/>, initialized once
/// per process), so this one answers "none" and holds no reference to any host's services.
/// </summary>
internal sealed class EmptyDynamicPropertyMetaDataResolver : IDynamicPropertyMetaDataResolver
{
    public static EmptyDynamicPropertyMetaDataResolver Instance { get; } = new();

    public Task<DynamicProperty> GetByNameAsync(string objectType, string propertyName) => Task.FromResult<DynamicProperty>(null);

    public Task<IList<DynamicProperty>> SearchAllAsync(string objectType) => Task.FromResult<IList<DynamicProperty>>([]);
}
