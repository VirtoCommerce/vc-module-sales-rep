using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E;

/// <summary>
/// The storefront's boot, without a browser: the in-process backend answers the storefront's OWN operation documents
/// (loaded from the checkout, fragments inlined) for a signed-in rep. A failure here is a backend-side shell gap, and
/// reads as a GraphQL error rather than as a blank page in the Playwright tests.
/// </summary>
[Trait("Category", "StorefrontE2E")]
public sealed class StorefrontBootContractTests
{
    /// <summary>What the in-process backend reports as installed; the storefront gates documents on this list.</summary>
    private static readonly string[] InstalledModules = ["VirtoCommerce.SalesRep", "VirtoCommerce.TaskManagement"];

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task Boot_AnswersTheStorefrontsOwnDocuments_ForASignedInRep()
    {
        await using var backend = await InProcessBackend.StartAsync();
        var context = backend.Context;

        await context.SeedOrganizationAsync("boot-org", org => org.Name = "Boot Org");
        var rep = await context.CreateRepInStoreAsync("Boot", "Rep", "boot.rep@e2e.local", InProcessBackend.StoreId, "boot-org");
        var token = await backend.Tokens.IssueAsync(rep.UserId);

        using var client = new HttpClient { BaseAddress = new System.Uri(backend.Url) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        // 1. InitializeApplication: the module list the storefront gates features on.
        var initialize = await PostAsync(client, "InitializeApplication", "store/queries/initializeApplication/initializeApplication.graphql",
            new { domain = "127.0.0.1" });
        var modules = initialize.GetProperty("data").GetProperty("store").GetProperty("settings").GetProperty("modules");
        modules.EnumerateArray().Select(x => x.GetProperty("moduleId").GetString())
            .Should().Contain(["VirtoCommerce.SalesRep", "VirtoCommerce.TaskManagement"]);

        // 2. GetPageContext: store + the signed-in user with the permissions the sales-rep hub is gated on.
        var pageContext = await PostAsync(client, "GetPageContext", "pageContext/queries/getPageContext/getPageContextQuery.graphql",
            new { userId = rep.UserId, domain = "127.0.0.1", permalink = "company/my-customers", cultureName = "en-US" });
        var user = pageContext.GetProperty("data").GetProperty("pageContext").GetProperty("user");
        user.GetProperty("userName").GetString().Should().Be("boot.rep@e2e.local");
        user.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).Should().Contain("sales-rep:access");
        user.GetProperty("contact").GetProperty("organizationId").GetString().Should().Be("boot-org");
        pageContext.GetProperty("data").GetProperty("pageContext").GetProperty("store").GetProperty("storeId").GetString().Should().Be(InProcessBackend.StoreId);

        // 3. The shell's other boot operations, which must at least validate and answer.
        await PostAsync(client, "GetMenu", "common/queries/getMenu/getMenu.graphql",
            new { storeId = InProcessBackend.StoreId, cultureName = "en-US", name = "footer-links" });
        await PostAsync(client, "GetShortCart", "cart/queries/getShortCart/getShortCartQuery.graphql",
            new { storeId = InProcessBackend.StoreId, userId = rep.UserId, currencyCode = "EUR", cultureName = "en-US" });
        await PostAsync(client, "GetSearchHistory", "common/queries/getSearchHistory/getSearchHistory.graphql",
            new { storeId = InProcessBackend.StoreId, maxCount = 5 });
        await PostAsync(client, "GetOrganizationAddresses", "organization/queries/getOrganizationAddresses/getOrganizationAddressesQuery.graphql",
            new { id = "boot-org", userId = rep.UserId, first = 10 });
    }

    private static async Task<JsonElement> PostAsync(HttpClient client, string operationName, string document, object variables)
    {
        var response = await client.PostAsJsonAsync("/graphql", new
        {
            operationName,
            query = StorefrontDocuments.Load(document, InstalledModules),
            variables,
        });
        var json = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"{operationName} must be accepted: {json}");
        json.Should().NotContain("\"errors\"", $"{operationName} must answer without errors");

        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
