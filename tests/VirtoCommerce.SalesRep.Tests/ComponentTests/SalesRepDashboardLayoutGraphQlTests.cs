using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// End-to-end component tests for the dashboard-layout X-API (save/load): execute the real
/// <c>saveSalesRepDashboardLayout</c> mutation and <c>salesRepDashboardLayout</c> query through the real scoped
/// schema / MediatR handlers / <c>DashboardLayoutService</c> over the real <c>CustomerPreference</c> store on
/// in-memory SQLite, and assert the layout round-trips. No mocks.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepDashboardLayoutGraphQlTests
{
    [Fact]
    public async Task SaveThenLoad_RoundTripsLayout()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var saveJson = await ctx.ExecuteGraphQlAsync(SaveMutation("dashboard"), userId: rep.UserId);
        saveJson.Should().NotContain("\"errors\"");

        var layout = Data(await ctx.ExecuteGraphQlAsync(LoadQuery("dashboard"), userId: rep.UserId));

        layout.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        layout.GetProperty("modifiedDate").ValueKind.Should().NotBe(JsonValueKind.Null); // stamped on save

        var regions = layout.GetProperty("regions").EnumerateArray().ToList();
        regions.Select(r => r.GetProperty("id").GetString()).Should().Equal("statistics", "mainLeft");

        // Array order is render order, and hidden is preserved.
        var statsBlocks = regions[0].GetProperty("blocks").EnumerateArray().ToList();
        statsBlocks.Select(b => b.GetProperty("id").GetString()).Should().Equal("b1", "b2");
        statsBlocks[0].GetProperty("type").GetString().Should().Be("stat");
        statsBlocks[0].GetProperty("hidden").GetBoolean().Should().BeFalse();
        statsBlocks[1].GetProperty("hidden").GetBoolean().Should().BeTrue();

        SettingValue(statsBlocks[0], "source").GetString().Should().Be("orders");
        SettingValue(statsBlocks[0], "period").GetString().Should().Be("MTD");

        // AnyValue keeps CLR types across the round-trip — a number stays a number, a bool a bool, not all strings.
        var recentOrders = regions[1].GetProperty("blocks")[0];
        recentOrders.GetProperty("type").GetString().Should().Be("recentOrders");
        SettingValue(recentOrders, "maxRows").ValueKind.Should().Be(JsonValueKind.Number);
        SettingValue(recentOrders, "maxRows").GetInt32().Should().Be(5);
        SettingValue(recentOrders, "sort").GetString().Should().Be("date:desc");
        SettingValue(recentOrders, "compact").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Load_WhenNeverSaved_ReturnsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(LoadQuery("dashboard"), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepDashboardLayout\":null"); // storefront then renders its registry default
    }

    [Fact]
    public async Task Layout_IsScopedPerUser_NoCrossUserLeak()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var repA = await ctx.CreateRepAsync("Alice", "Rep", "alice@test.com", "org-1");
        var repB = await ctx.CreateRepAsync("Bob", "Rep", "bob@test.com", "org-1");

        await ctx.ExecuteGraphQlAsync(SaveMutation("dashboard"), userId: repA.UserId);

        // B never saved a layout; A's must not leak to B — the key is the caller's own user id (data-isolation invariant).
        var json = await ctx.ExecuteGraphQlAsync(LoadQuery("dashboard"), userId: repB.UserId);
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepDashboardLayout\":null");
    }

    [Fact]
    public async Task Layout_ScopesAreIndependent()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        await ctx.ExecuteGraphQlAsync(SaveMutation("dashboard"), userId: rep.UserId);

        // customerProfile was never saved — null even though `dashboard` exists for the same user.
        var customerProfile = await ctx.ExecuteGraphQlAsync(LoadQuery("customerProfile"), userId: rep.UserId);
        customerProfile.Should().Contain("\"salesRepDashboardLayout\":null");

        // The dashboard surface still loads independently.
        Data(await ctx.ExecuteGraphQlAsync(LoadQuery("dashboard"), userId: rep.UserId))
            .GetProperty("regions").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Layout_IsScopedByStore()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        await ctx.ExecuteGraphQlAsync(SaveMutation("dashboard", storeId: "B2B-store"), userId: rep.UserId);

        // Same scope + user, different store → a separate key, so nothing is saved there.
        var otherStore = await ctx.ExecuteGraphQlAsync(LoadQuery("dashboard", storeId: "OtherStore"), userId: rep.UserId);
        otherStore.Should().Contain("\"salesRepDashboardLayout\":null");

        // The store it was saved under still loads.
        Data(await ctx.ExecuteGraphQlAsync(LoadQuery("dashboard", storeId: "B2B-store"), userId: rep.UserId))
            .GetProperty("regions").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Save_ReplacesPreviousLayout()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        await ctx.ExecuteGraphQlAsync(SaveMutation("dashboard"), userId: rep.UserId);

        // A second save (same key) fully replaces the first — not a merge.
        var replacement = """
            mutation {
              saveSalesRepDashboardLayout(command: {
                scope: "dashboard", storeId: "B2B-store", schemaVersion: 2
                regions: [ { id: "statistics", blocks: [ { id: "only", type: "news", hidden: false, settings: [] } ] } ]
              }) { schemaVersion regions { id blocks { id } } }
            }
            """;
        (await ctx.ExecuteGraphQlAsync(replacement, userId: rep.UserId)).Should().NotContain("\"errors\"");

        var layout = Data(await ctx.ExecuteGraphQlAsync(LoadQuery("dashboard"), userId: rep.UserId));
        layout.GetProperty("schemaVersion").GetInt32().Should().Be(2);
        var regions = layout.GetProperty("regions").EnumerateArray().ToList();
        regions.Should().ContainSingle();
        regions[0].GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("id").GetString()).Should().Equal("only");
    }

    [Fact]
    public async Task Load_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(LoadQuery("dashboard"));

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task Save_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(SaveMutation("dashboard"));

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    // ---- helpers ----

    // A representative layout: two regions, a hidden block, empty settings, and string/number/bool setting values.
    private static string SaveMutation(string scope, string storeId = "B2B-store") => $$"""
        mutation {
          saveSalesRepDashboardLayout(command: {
            scope: "{{scope}}"
            storeId: "{{storeId}}"
            schemaVersion: 1
            regions: [
              { id: "statistics", blocks: [
                { id: "b1", type: "stat", hidden: false, settings: [
                  { key: "source", value: "orders" },
                  { key: "measure", value: "count" },
                  { key: "period", value: "MTD" }
                ] },
                { id: "b2", type: "stat", hidden: true, settings: [] }
              ] },
              { id: "mainLeft", blocks: [
                { id: "b3", type: "recentOrders", hidden: false, settings: [
                  { key: "maxRows", value: 5 },
                  { key: "sort", value: "date:desc" },
                  { key: "compact", value: true }
                ] }
              ] }
            ]
          }) {
            schemaVersion modifiedDate
            regions { id blocks { id type hidden settings { key value } } }
          }
        }
        """;

    private static string LoadQuery(string scope, string storeId = "B2B-store") => $$"""
        query {
          salesRepDashboardLayout(scope: "{{scope}}", storeId: "{{storeId}}") {
            schemaVersion modifiedDate
            regions { id blocks { id type hidden settings { key value } } }
          }
        }
        """;

    /// <summary>The <c>data.salesRepDashboardLayout</c> node, after asserting the response carries no errors.</summary>
    private static JsonElement Data(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("errors", out _).Should().BeFalse("GraphQL response should carry no errors: {0}", json);
        return root.GetProperty("data").GetProperty("salesRepDashboardLayout").Clone();
    }

    /// <summary>The <c>value</c> of a block setting by key (fails if the key is absent).</summary>
    private static JsonElement SettingValue(JsonElement block, string key)
        => block.GetProperty("settings").EnumerateArray().Single(s => s.GetProperty("key").GetString() == key).GetProperty("value");
}
