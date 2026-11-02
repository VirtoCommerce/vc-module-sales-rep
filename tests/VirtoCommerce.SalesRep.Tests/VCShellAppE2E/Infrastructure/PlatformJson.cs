using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using VirtoCommerce.Platform.Core.JsonConverters;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>
/// The platform's REST JSON contract (what Platform.Web's Startup passes to AddNewtonsoftJson): camelCase through
/// the polymorphic resolver, string enums, nulls omitted, UTC dates. Applied to the hosted module controllers and
/// used by the replica endpoints, so the app reads every response the same way it does in production.
/// </summary>
internal static class PlatformJson
{
    public static JsonSerializerSettings Settings { get; } = Configure(new JsonSerializerSettings());

    public static JsonSerializerSettings Configure(JsonSerializerSettings settings)
    {
        settings.ContractResolver = new PolymorphJsonContractResolver();
        settings.Converters.Add(new StringEnumConverter());
        // The two converters Startup.Configure adds at runtime: dynamic property values by name, polymorphic
        // materialization of overridden types.
        settings.Converters.Add(new DynamicObjectPropertyJsonConverter(EmptyDynamicPropertyMetaDataResolver.Instance));
        settings.Converters.Add(new PolymorphJsonConverter());
        settings.PreserveReferencesHandling = PreserveReferencesHandling.None;
        settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
        settings.NullValueHandling = NullValueHandling.Ignore;
        settings.Formatting = Formatting.None;
        return settings;
    }

    public static void Configure(MvcNewtonsoftJsonOptions options) => Configure(options.SerializerSettings);

    public static IResult Result(object value)
        => Results.Content(JsonConvert.SerializeObject(value, Settings), "application/json");

    public static async Task<T> ReadAsync<T>(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        return JsonConvert.DeserializeObject<T>(await reader.ReadToEndAsync(), Settings);
    }
}
