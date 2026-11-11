using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// A GraphQL request as the storefront's Apollo client sends one: a POST to /graphql with the content type
/// application/json and nothing after it, the only JSON type X-API's content type rule accepts.
/// </summary>
internal static class GraphQlHttp
{
    public static Task<HttpResponseMessage> PostAsync(HttpClient client, object body)
        => client.PostAsync("/graphql", JsonContent.Create(body, new MediaTypeHeaderValue("application/json")));
}
