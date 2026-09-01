using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The sales-rep task X-API (VCST-5732) end to end: real GraphQL strings through the real scoped schema and the real
/// vc-module-task-management services over in-memory SQLite.
///
/// The point of this suite is the negative half. Task ownership is the whole security boundary - there is no
/// organization scoping and no dedicated permission - so every surface is checked against another rep's task, an
/// account with no contact, a non-rep, an administrator, and a deployment with the module absent.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepTasksGraphQlTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private static readonly DateTime Today = new(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string TodayIso = Iso(Today);

    [Fact]
    public async Task CreateSalesRepTask_StampsTheCallerAsOwner_AndTheTaskComesBackInTheirList()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        var created = await CreateTaskAsync(ctx, rep, "Renew Cabin Co. contract", Today.AddDays(2), priority: "High", type: "Other", description: "Escalate to regional manager.");

        created.GetProperty("name").GetString().Should().Be("Renew Cabin Co. contract");
        created.GetProperty("priority").GetString().Should().Be("High");
        created.GetProperty("description").GetString().Should().Be("Escalate to regional manager.");
        created.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var list = await ListTasksAsync(ctx, rep);
        list.GetProperty("totalCount").GetInt32().Should().Be(1);
        list.GetProperty("items")[0].GetProperty("id").GetString().Should().Be(created.GetProperty("id").GetString());
    }

    [Fact]
    public async Task CreateSalesRepTask_TrimsTheName()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        var created = await CreateTaskAsync(ctx, rep, "   Padded   ", Today);

        created.GetProperty("name").GetString().Should().Be("Padded");
    }

    [Fact]
    public async Task SalesRepTasks_DoNotLeakAnotherRepsTasks()
    {
        using var ctx = SalesRepTestContext.Create();
        var ann = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);
        var bob = await SeedRepAsync(ctx, "Bob", "Rep", "bob@test.com", OrgA);

        var annTask = await CreateTaskAsync(ctx, ann, "Ann private task", Today);
        await CreateTaskAsync(ctx, bob, "Bob private task", Today);

        var annList = await ListTasksAsync(ctx, ann);
        annList.GetProperty("totalCount").GetInt32().Should().Be(1);
        annList.GetProperty("items")[0].GetProperty("name").GetString().Should().Be("Ann private task");

        // The whole response, not just the parsed node: nothing about Bob's task may appear anywhere.
        var raw = await QueryAsync(ctx, ann, "salesRepTasks { totalCount items { id name description } }");
        raw.Should().NotContain("Bob private task");

        // And by id: another rep's task must be indistinguishable from one that does not exist.
        var bobTaskId = SalesRepTestContext.Node(
            await QueryAsync(ctx, bob, "salesRepTasks { items { id } }"), "salesRepTasks")
            .GetProperty("items")[0].GetProperty("id").GetString();

        bobTaskId.Should().NotBe(annTask.GetProperty("id").GetString());

        var byId = SalesRepTestContext.Node(await QueryAsync(ctx, ann, $"salesRepTask(id: \"{bobTaskId}\") {{ id name }}"), "salesRepTask");
        byId.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Mutations_OnAnotherRepsTask_AreForbidden_AndChangeNothing()
    {
        using var ctx = SalesRepTestContext.Create();
        var ann = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);
        var bob = await SeedRepAsync(ctx, "Bob", "Rep", "bob@test.com", OrgA);

        var bobTask = await CreateTaskAsync(ctx, bob, "Bob private task", Today);
        var bobTaskId = bobTask.GetProperty("id").GetString();

        var mutations = new[]
        {
            $"updateSalesRepTask(command: {{ id: \"{bobTaskId}\", name: \"Hijacked\", dueDate: \"{TodayIso}\" }}) {{ id name }}",
            $"changeSalesRepTaskStatus(command: {{ id: \"{bobTaskId}\", completed: true }}) {{ id completed }}",
            $"deleteSalesRepTask(command: {{ id: \"{bobTaskId}\" }})",
        };

        foreach (var mutation in mutations)
        {
            var json = await MutateAsync(ctx, ann, mutation);

            json.Should().Contain("\"errors\"");
            json.Should().NotContain("Bob private task");
        }

        // Bob's task is untouched: same name, still active, still there.
        var bobList = await ListTasksAsync(ctx, bob);
        bobList.GetProperty("totalCount").GetInt32().Should().Be(1);
        var survivor = bobList.GetProperty("items")[0];
        survivor.GetProperty("name").GetString().Should().Be("Bob private task");
        survivor.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task AllSurfaces_Anonymous_AreDenied()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);
        var task = await CreateTaskAsync(ctx, rep, "Secret task", Today);
        var taskId = task.GetProperty("id").GetString();

        var operations = new[]
        {
            "query { salesRepTasks { totalCount items { id name } } }",
            $"query {{ salesRepTask(id: \"{taskId}\") {{ id name }} }}",
            "query { salesRepTaskFilterRules { name } }",
            "query { salesRepTaskSortRules { name } }",
            "query { salesRepTaskTypes }",
            $"mutation {{ createSalesRepTask(command: {{ name: \"X\", dueDate: \"{TodayIso}\" }}) {{ id }} }}",
            $"mutation {{ updateSalesRepTask(command: {{ id: \"{taskId}\", name: \"X\", dueDate: \"{TodayIso}\" }}) {{ id }} }}",
            $"mutation {{ changeSalesRepTaskStatus(command: {{ id: \"{taskId}\", completed: true }}) {{ id }} }}",
            $"mutation {{ deleteSalesRepTask(command: {{ id: \"{taskId}\" }}) }}",
        };

        foreach (var operation in operations)
        {
            var json = await ctx.ExecuteGraphQlAnonymousAsync(operation);

            json.Should().Contain("\"errors\"");
            json.Should().MatchRegex("(?i)anonym");
            json.Should().NotContain("Secret task");
        }
    }

    [Fact]
    public async Task AccountWithNoContact_ReadsNothing_AndCannotCreate()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);
        await CreateTaskAsync(ctx, rep, "Ann private task", Today);

        // The platform writes the claim as `user.MemberId ?? string.Empty`, so an account with no contact arrives
        // with an EMPTY claim rather than none. Both must read as "no member", never as "no filter".
        foreach (var memberId in new[] { null, string.Empty })
        {
            var json = await ctx.ExecuteGraphQlAsync(
                "query { salesRepTasks { totalCount items { id name } } }",
                userId: rep.UserId,
                memberId: memberId);

            var node = SalesRepTestContext.Node(json, "salesRepTasks");
            node.GetProperty("totalCount").GetInt32().Should().Be(0);
            json.Should().NotContain("Ann private task");

            var created = await ctx.ExecuteGraphQlAsync(
                $"mutation {{ createSalesRepTask(command: {{ name: \"Orphan\", dueDate: \"{TodayIso}\" }}) {{ id }} }}",
                userId: rep.UserId,
                memberId: memberId);

            created.Should().Contain("\"errors\"");
            created.Should().MatchRegex("(?i)no contact");
        }
    }

    [Fact]
    public async Task NonRep_ReadsNothing_AndCannotCreate()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);
        await CreateTaskAsync(ctx, rep, "Ann private task", Today);

        var outsiderContact = await ctx.SeedContactAsync("outsider-contact");
        var outsiderUserId = await ctx.CreateAccountWithoutRolesAsync(outsiderContact.Id, "outsider@test.com");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepTasks { totalCount items { id name } } }",
            userId: outsiderUserId,
            memberId: outsiderContact.Id);

        SalesRepTestContext.Node(json, "salesRepTasks").GetProperty("totalCount").GetInt32().Should().Be(0);
        json.Should().NotContain("Ann private task");

        // Rule vocabulary is rep-only too.
        SalesRepTestContext.Node(
            await ctx.ExecuteGraphQlAsync("query { salesRepTaskFilterRules { name } }", userId: outsiderUserId, memberId: outsiderContact.Id),
            "salesRepTaskFilterRules").GetArrayLength().Should().Be(0);

        var created = await ctx.ExecuteGraphQlAsync(
            $"mutation {{ createSalesRepTask(command: {{ name: \"Intruder\", dueDate: \"{TodayIso}\" }}) {{ id }} }}",
            userId: outsiderUserId,
            memberId: outsiderContact.Id);

        created.Should().Contain("\"errors\"");
    }

    [Fact]
    public async Task Administrator_IsNotABackdoorIntoAnotherRepsTasks()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);
        await CreateTaskAsync(ctx, rep, "Ann private task", Today);

        var adminContact = await ctx.SeedContactAsync("admin-contact");
        var adminUserId = await ctx.CreateAccountWithoutRolesAsync(adminContact.Id, "admin@test.com");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepTasks { totalCount items { id name } } }",
            userId: adminUserId,
            memberId: adminContact.Id,
            isAdministrator: true);

        // Tasks are scoped by the caller's own contact, so an administrator simply sees their own (none).
        SalesRepTestContext.Node(json, "salesRepTasks").GetProperty("totalCount").GetInt32().Should().Be(0);
        json.Should().NotContain("Ann private task");
    }

    [Fact]
    public async Task UnknownFilterRule_FailsClosed()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);
        await CreateTaskAsync(ctx, rep, "Ann private task", Today);

        var json = await QueryAsync(ctx, rep, "salesRepTasks(filter: \"not-a-rule\") { totalCount items { id name } }");

        SalesRepTestContext.Node(json, "salesRepTasks").GetProperty("totalCount").GetInt32().Should().Be(0);
        json.Should().NotContain("Ann private task");
    }

    [Fact]
    public async Task FilterRules_SplitUpcomingOverdueAndCompleted_OnTheCallersDayBoundary()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        await CreateTaskAsync(ctx, rep, "Overdue task", Today.AddDays(-3));
        await CreateTaskAsync(ctx, rep, "Due exactly at midnight", Today);
        await CreateTaskAsync(ctx, rep, "Upcoming task", Today.AddDays(4));
        var done = await CreateTaskAsync(ctx, rep, "Finished task", Today.AddDays(1));
        await MutateAsync(ctx, rep, $"changeSalesRepTaskStatus(command: {{ id: \"{done.GetProperty("id").GetString()}\", completed: true }}) {{ id completed isActive }}");

        (await NamesForFilterAsync(ctx, rep, "overdue")).Should().Equal("Overdue task");

        // A task due at exactly 00:00 belongs to today, so it reads as upcoming, not overdue.
        (await NamesForFilterAsync(ctx, rep, "upcoming")).Should().BeEquivalentTo("Due exactly at midnight", "Upcoming task");

        (await NamesForFilterAsync(ctx, rep, "completed")).Should().Equal("Finished task");

        // The three tabs partition the list: they sum to the unfiltered total.
        var all = await ListTasksAsync(ctx, rep);
        all.GetProperty("totalCount").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task MultiOrgRep_SeesOneTaskList_UnaffectedByOrganization()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA, OrgB);

        await CreateTaskAsync(ctx, rep, "Task one", Today);
        await CreateTaskAsync(ctx, rep, "Task two", Today.AddDays(1));

        // A task belongs to a person, not an organization - serving two orgs must not split or duplicate the list.
        var list = await ListTasksAsync(ctx, rep);
        list.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task WithoutTaskManagementModule_ReadsAreEmpty_AndWritesFailCleanly()
    {
        using var ctx = SalesRepTestContext.Create(withTaskManagement: false);
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        // The schema still carries the fields (it must not differ per deployment - the frontend generates types
        // against a live endpoint); they just answer empty.
        var list = await ListTasksAsync(ctx, rep);
        list.GetProperty("totalCount").GetInt32().Should().Be(0);

        SalesRepTestContext.Node(
            await QueryAsync(ctx, rep, "salesRepTaskTypes"), "salesRepTaskTypes").GetArrayLength().Should().Be(0);

        var created = await MutateAsync(ctx, rep, $"createSalesRepTask(command: {{ name: \"X\", dueDate: \"{TodayIso}\" }}) {{ id }}");
        created.Should().Contain("\"errors\"");
        created.Should().MatchRegex("(?i)not available");
    }

    [Fact]
    public async Task SortRules_RejectAnUnsupportedDirection_AndDefaultToDueDate()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        await CreateTaskAsync(ctx, rep, "Later", Today.AddDays(5));
        await CreateTaskAsync(ctx, rep, "Sooner", Today.AddDays(1));

        // Default rule is due-date ascending: soonest first.
        var defaultOrder = await ListTasksAsync(ctx, rep);
        Names(defaultOrder).Should().Equal("Sooner", "Later");

        var reversed = SalesRepTestContext.Node(
            await QueryAsync(ctx, rep, "salesRepTasks(sort: \"due-date:desc\") { items { name } }"), "salesRepTasks");
        Names(reversed).Should().Equal("Later", "Sooner");

        // `recent` is one-way; asking for the opposite direction is an error, not a silent fallback.
        var json = await QueryAsync(ctx, rep, "salesRepTasks(sort: \"recent:asc\") { items { name } }");
        json.Should().Contain("\"errors\"");
    }

    // -- helpers -------------------------------------------------------------------------------------------------

    private sealed record Rep(string UserId, string MemberId);

    private static string Iso(DateTime value) => value.ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static string[] Names(System.Text.Json.JsonElement node) =>
        node.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToArray();

    private static async Task<Rep> SeedRepAsync(SalesRepTestContext ctx, string firstName, string lastName, string email, params string[] organizationIds)
    {
        await ctx.SeedOrganizationsAsync(organizationIds);
        var details = await ctx.CreateRepAsync(firstName, lastName, email, organizationIds);

        return new Rep(details.UserId, details.Id);
    }

    private static Task<string> QueryAsync(SalesRepTestContext ctx, Rep rep, string selection) =>
        ctx.ExecuteGraphQlAsync($"query {{ {selection} }}", userId: rep.UserId, memberId: rep.MemberId);

    private static Task<string> MutateAsync(SalesRepTestContext ctx, Rep rep, string selection) =>
        ctx.ExecuteGraphQlAsync($"mutation {{ {selection} }}", userId: rep.UserId, memberId: rep.MemberId);

    private static async Task<System.Text.Json.JsonElement> ListTasksAsync(SalesRepTestContext ctx, Rep rep)
    {
        var json = await QueryAsync(ctx, rep, "salesRepTasks { totalCount items { id name isActive completed dueDate } }");

        return SalesRepTestContext.Node(json, "salesRepTasks");
    }

    private static async Task<string[]> NamesForFilterAsync(SalesRepTestContext ctx, Rep rep, string filter)
    {
        var json = await QueryAsync(ctx, rep, $"salesRepTasks(filter: \"{filter}\", today: \"{TodayIso}\") {{ items {{ name }} }}");

        return Names(SalesRepTestContext.Node(json, "salesRepTasks"));
    }

    private static async Task<System.Text.Json.JsonElement> CreateTaskAsync(
        SalesRepTestContext ctx,
        Rep rep,
        string name,
        DateTime dueDate,
        string priority = null,
        string type = null,
        string description = null)
    {
        var fields = $"name: \"{name}\", dueDate: \"{Iso(dueDate)}\"";
        if (priority != null)
        {
            fields += $", priority: \"{priority}\"";
        }

        if (type != null)
        {
            fields += $", type: \"{type}\"";
        }

        if (description != null)
        {
            fields += $", description: \"{description}\"";
        }

        var json = await MutateAsync(ctx, rep, $"createSalesRepTask(command: {{ {fields} }}) {{ id name description priority dueDate isActive completed }}");

        return SalesRepTestContext.Node(json, "createSalesRepTask");
    }
}
