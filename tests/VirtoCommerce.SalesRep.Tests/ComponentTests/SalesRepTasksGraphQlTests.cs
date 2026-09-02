using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.TaskManagement.Core.Models;
using VirtoCommerce.TaskManagement.Core.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

// The task X-API end to end: real GraphQL through the real scoped schema and the real task-management services
// over in-memory SQLite. Ownership is the whole security boundary - no organization scoping, no dedicated
// permission - so every surface is also checked against another rep, a non-rep, an admin and a missing module.
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
            $"mutation {{ updateSalesRepTask(command: {{ id: \"{taskId}\", name: \"X\", dueDate: \"{TodayIso}\", description: \"\", type: \"\", priority: \"\" }}) {{ id }} }}",
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

        // An account with no contact arrives with an EMPTY claim, not none. Both must read as "no member".
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

        // Rule vocabulary is rep-only too - both kinds, which reach the scope check by different paths.
        foreach (var rules in new[] { "salesRepTaskFilterRules", "salesRepTaskSortRules" })
        {
            SalesRepTestContext.Node(
                await ctx.ExecuteGraphQlAsync($"query {{ {rules} {{ name }} }}", userId: outsiderUserId, memberId: outsiderContact.Id),
                rules).GetArrayLength().Should().Be(0);
        }

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

        // The three tabs happen to cover everything HERE only because this API always writes a due date and never
        // cancels; see TasksOutsideEveryTab_StayInTheUnfilteredList for the rows that fall through.
        var all = await ListTasksAsync(ctx, rep);
        all.GetProperty("totalCount").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task TasksOutsideEveryTab_StayInTheUnfilteredList()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        await CreateTaskAsync(ctx, rep, "Has a due date", Today.AddDays(1));

        // Neither shape is reachable through this API - both arrive from the admin UI, the REST API or a workflow.
        await SaveTaskDirectlyAsync(ctx, rep, "No due date", dueDate: null, isActive: true, completed: null);
        await SaveTaskDirectlyAsync(ctx, rep, "Canceled", dueDate: Today.AddDays(1), isActive: false, completed: false);

        // The upstream criteria bound the due date with >= / <=, which drop NULLs, and `completed` means finished as
        // done - so neither row matches any rule. Pinned, because it means the tab counts do NOT sum to the total:
        // making them sum needs a "no due date" flag on WorkTaskSearchCriteria upstream, not a change here.
        (await NamesForFilterAsync(ctx, rep, "upcoming")).Should().Equal("Has a due date");
        (await NamesForFilterAsync(ctx, rep, "overdue")).Should().BeEmpty();
        (await NamesForFilterAsync(ctx, rep, "completed")).Should().BeEmpty();

        // They are still the rep's tasks, so the unfiltered list keeps them visible rather than hiding work.
        Names(await ListTasksAsync(ctx, rep)).Should().BeEquivalentTo("Has a due date", "No due date", "Canceled");
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

        // The schema must not differ per deployment (the frontend generates types against a live endpoint).
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

    [Fact]
    public async Task UpdateAndDelete_OnTheCallersOwnTask_Succeed()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        var created = await CreateTaskAsync(ctx, rep, "Draft", Today, priority: "Low", description: "First pass.");
        var taskId = created.GetProperty("id").GetString();

        // Worth asserting on its own: every other mutation test expects a DENIAL, which a handler that lost the
        // caller's identity would satisfy too.
        var updated = SalesRepTestContext.Node(
            await UpdateTaskAsync(ctx, rep, taskId, "  Revised  ", Today.AddDays(3), description: "Second pass.", priority: "High"),
            "updateSalesRepTask");

        updated.GetProperty("id").GetString().Should().Be(taskId);
        updated.GetProperty("name").GetString().Should().Be("Revised");
        updated.GetProperty("priority").GetString().Should().Be("High");
        updated.GetProperty("description").GetString().Should().Be("Second pass.");

        var deleted = SalesRepTestContext.Node(
            await MutateAsync(ctx, rep, $"deleteSalesRepTask(command: {{ id: \"{taskId}\" }})"), "deleteSalesRepTask");

        deleted.GetBoolean().Should().BeTrue();
        (await ListTasksAsync(ctx, rep)).GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ChangeStatus_CompletesATask_AndPutsItBack()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        var created = await CreateTaskAsync(ctx, rep, "Follow up", Today.AddDays(1));
        var taskId = created.GetProperty("id").GetString();

        // A plain save, not FinishAsync - which publishes a cancellation event even when completing, and cannot
        // reopen.
        var completed = SalesRepTestContext.Node(
            await MutateAsync(ctx, rep, $"changeSalesRepTaskStatus(command: {{ id: \"{taskId}\", completed: true }}) {{ id completed isActive }}"),
            "changeSalesRepTaskStatus");

        completed.GetProperty("completed").GetBoolean().Should().BeTrue();
        completed.GetProperty("isActive").GetBoolean().Should().BeFalse();
        (await NamesForFilterAsync(ctx, rep, "completed")).Should().Equal("Follow up");

        var reopened = SalesRepTestContext.Node(
            await MutateAsync(ctx, rep, $"changeSalesRepTaskStatus(command: {{ id: \"{taskId}\", completed: false }}) {{ id completed isActive }}"),
            "changeSalesRepTaskStatus");

        reopened.GetProperty("completed").GetBoolean().Should().BeFalse();
        reopened.GetProperty("isActive").GetBoolean().Should().BeTrue();
        (await NamesForFilterAsync(ctx, rep, "completed")).Should().BeEmpty();
        (await NamesForFilterAsync(ctx, rep, "upcoming")).Should().Equal("Follow up");
    }

    [Fact]
    public async Task Period_ScopesToADayWindow_AndIntersectsWithTheFilter()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        await CreateTaskAsync(ctx, rep, "Yesterday", Today.AddDays(-1));
        await CreateTaskAsync(ctx, rep, "Today", Today);
        await CreateTaskAsync(ctx, rep, "Tomorrow", Today.AddDays(1));

        var day = $"period: {{ from: \"{Iso(Today)}\", to: \"{Iso(Today.AddDays(1).AddSeconds(-1))}\" }}";

        // The Calendar page sends both, so the filter has to NARROW the window rather than replace it.
        (await NamesForAsync(ctx, rep, day)).Should().Equal("Today");
        (await NamesForAsync(ctx, rep, $"{day}, filter: \"upcoming\"")).Should().Equal("Today");
        (await NamesForAsync(ctx, rep, $"{day}, filter: \"overdue\"")).Should().BeEmpty();

        // Same tab without the window still reaches the day that is genuinely overdue.
        (await NamesForFilterAsync(ctx, rep, "overdue")).Should().Equal("Yesterday");
    }

    [Fact]
    public async Task Paging_TakesTheOffsetAsTheCursor()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        await CreateTaskAsync(ctx, rep, "First", Today.AddDays(1));
        await CreateTaskAsync(ctx, rep, "Second", Today.AddDays(2));
        await CreateTaskAsync(ctx, rep, "Third", Today.AddDays(3));

        // xAPI connections take the offset as the cursor. The total stays the whole list, not the page.
        var firstPage = SalesRepTestContext.Node(
            await QueryAsync(ctx, rep, "salesRepTasks(first: 2, after: \"0\") { totalCount items { name } }"), "salesRepTasks");

        firstPage.GetProperty("totalCount").GetInt32().Should().Be(3);
        Names(firstPage).Should().Equal("First", "Second");

        var secondPage = SalesRepTestContext.Node(
            await QueryAsync(ctx, rep, "salesRepTasks(first: 2, after: \"2\") { totalCount items { name } }"), "salesRepTasks");

        secondPage.GetProperty("totalCount").GetInt32().Should().Be(3);
        Names(secondPage).Should().Equal("Third");
    }

    [Fact]
    public async Task UnknownPriority_IsRejected_RatherThanSilentlyDefaulted()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        // Strict on purpose: the module's own SafeParse would quietly store Normal and the rep would never know.
        var json = await MutateAsync(ctx, rep, $"createSalesRepTask(command: {{ name: \"Typo\", dueDate: \"{TodayIso}\", priority: \"Urgent\" }}) {{ id priority }}");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)priority");

        (await ListTasksAsync(ctx, rep)).GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task WriteInputs_ExposeNoIdentityFields_SoOwnershipCannotComeFromTheClient()
    {
        using var ctx = SalesRepTestContext.Create();
        var ann = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);
        var bob = await SeedRepAsync(ctx, "Bob", "Rep", "bob@test.com", OrgA);

        var annTask = await CreateTaskAsync(ctx, ann, "Ann private task", Today);
        var annTaskId = annTask.GetProperty("id").GetString();

        // The inputs carry no field that could override the stamped owner; adding one would be a silent takeover.
        var attempts = new (string Field, string Mutation)[]
        {
            ("responsibleId", $"createSalesRepTask(command: {{ name: \"Planted\", dueDate: \"{TodayIso}\", responsibleId: \"{bob.MemberId}\" }}) {{ id }}"),
            ("memberId", $"createSalesRepTask(command: {{ name: \"Planted\", dueDate: \"{TodayIso}\", memberId: \"{bob.MemberId}\" }}) {{ id }}"),
            ("userId", $"createSalesRepTask(command: {{ name: \"Planted\", dueDate: \"{TodayIso}\", userId: \"{bob.UserId}\" }}) {{ id }}"),
            ("responsibleId", $"updateSalesRepTask(command: {{ id: \"{annTaskId}\", name: \"Reassigned\", dueDate: \"{TodayIso}\", description: \"\", type: \"\", priority: \"\", responsibleId: \"{bob.MemberId}\" }}) {{ id }}"),
        };

        foreach (var (field, mutation) in attempts)
        {
            var json = await MutateAsync(ctx, ann, mutation);

            // Named in the error, so this cannot pass on some unrelated failure.
            json.Should().Contain("\"errors\"");
            json.Should().Contain(field);
        }

        (await ListTasksAsync(ctx, bob)).GetProperty("totalCount").GetInt32().Should().Be(0);
        Names(await ListTasksAsync(ctx, ann)).Should().Equal("Ann private task");
    }

    [Fact]
    public async Task SalesRepTask_ById_IsNullForAnIdThatDoesNotExist()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);
        await CreateTaskAsync(ctx, rep, "Ann private task", Today);

        // The other half of the isolation test: a missing id and someone else's must answer identically.
        var byId = SalesRepTestContext.Node(
            await QueryAsync(ctx, rep, "salesRepTask(id: \"no-such-task\") { id name }"), "salesRepTask");

        byId.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task UpdateSalesRepTask_ReplacesEveryEditableField_AndCannotOmitOne()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        var created = await CreateTaskAsync(ctx, rep, "Draft", Today, priority: "High", type: "Call", description: "First pass.");
        var taskId = created.GetProperty("id").GetString();

        // The update REPLACES: every editable field is non-null, so a rename cannot silently drop the description,
        // the type or the priority the way an omitted optional field would.
        var omitted = await MutateAsync(ctx, rep, $"updateSalesRepTask(command: {{ id: \"{taskId}\", name: \"Renamed\", dueDate: \"{TodayIso}\" }}) {{ id }}");
        omitted.Should().Contain("\"errors\"");
        omitted.Should().Contain("description");

        var stillThere = SalesRepTestContext.Node(
            await QueryAsync(ctx, rep, $"salesRepTask(id: \"{taskId}\") {{ name description type priority }}"), "salesRepTask");
        stillThere.GetProperty("name").GetString().Should().Be("Draft");
        stillThere.GetProperty("description").GetString().Should().Be("First pass.");
        stillThere.GetProperty("type").GetString().Should().Be("Call");
        stillThere.GetProperty("priority").GetString().Should().Be("High");

        // Clearing is explicit, and blank collapses to null so a cleared field reads like one never set.
        var cleared = SalesRepTestContext.Node(
            await UpdateTaskAsync(ctx, rep, taskId, "Renamed", Today, description: "", type: "", priority: ""),
            "updateSalesRepTask");
        cleared.GetProperty("name").GetString().Should().Be("Renamed");
        cleared.GetProperty("description").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        cleared.GetProperty("type").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        cleared.GetProperty("priority").GetString().Should().Be("Normal");
    }

    [Fact]
    public async Task UpdateSalesRepTask_LeavesTheCompletionStateAlone()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        var created = await CreateTaskAsync(ctx, rep, "Draft", Today.AddDays(1));
        var taskId = created.GetProperty("id").GetString();
        await MutateAsync(ctx, rep, $"changeSalesRepTaskStatus(command: {{ id: \"{taskId}\", completed: true }}) {{ id }}");

        // "Replaces" is scoped to the EDITABLE fields. Completion is not one of them - it moves only through
        // changeSalesRepTaskStatus - so editing a finished task must not quietly reopen it.
        var updated = SalesRepTestContext.Node(
            await MutateAsync(
                ctx,
                rep,
                $"updateSalesRepTask(command: {{ id: \"{taskId}\", name: \"Renamed after finishing\", dueDate: \"{Iso(Today.AddDays(2))}\", description: \"Second pass.\", type: \"\", priority: \"High\" }}) {{ name isActive completed }}"),
            "updateSalesRepTask");

        updated.GetProperty("name").GetString().Should().Be("Renamed after finishing");
        updated.GetProperty("isActive").GetBoolean().Should().BeFalse();
        updated.GetProperty("completed").GetBoolean().Should().BeTrue();

        // Persisted, not just echoed back.
        var stored = await ctx.GetRequiredService<IWorkTaskService>().GetByIdAsync(taskId);
        stored.IsActive.Should().BeFalse();
        stored.Completed.Should().Be(true);

        // And the derived status reads off that pair, so the task stays on Completed instead of reappearing as
        // work still to do.
        (await NamesForFilterAsync(ctx, rep, "completed")).Should().Equal("Renamed after finishing");
        (await NamesForFilterAsync(ctx, rep, "upcoming")).Should().BeEmpty();
    }

    [Fact]
    public async Task CreateSalesRepTask_TakesTheStoreFromTheCallersAccount_NotFromTheInput()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync(OrgA);
        var details = await ctx.CreateRepInStoreAsync("Ann", "Rep", "ann@test.com", "store-a", OrgA);
        var rep = new Rep(details.UserId, details.Id);

        // Not an input field at all - the store a task belongs to is part of who owns it.
        var planted = await MutateAsync(ctx, rep, $"createSalesRepTask(command: {{ name: \"Planted\", dueDate: \"{TodayIso}\", storeId: \"store-b\" }}) {{ id }}");
        planted.Should().Contain("\"errors\"");
        planted.Should().Contain("storeId");

        await CreateTaskAsync(ctx, rep, "Stamped", Today);

        // Stamped from the rep's own account store, so the store filter reaches it and another store does not.
        Names(await ListTasksAsync(ctx, rep, "storeId: \"store-a\"")).Should().Equal("Stamped");
        Names(await ListTasksAsync(ctx, rep, "storeId: \"store-b\"")).Should().BeEmpty();
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

    private static async Task<System.Text.Json.JsonElement> ListTasksAsync(SalesRepTestContext ctx, Rep rep, string arguments = null)
    {
        var call = arguments == null ? "salesRepTasks" : $"salesRepTasks({arguments})";
        var json = await QueryAsync(ctx, rep, $"{call} {{ totalCount items {{ id name isActive completed dueDate }} }}");

        return SalesRepTestContext.Node(json, "salesRepTasks");
    }

    /// <summary>Every editable field, because the update replaces rather than patches.</summary>
    private static Task<string> UpdateTaskAsync(
        SalesRepTestContext ctx,
        Rep rep,
        string id,
        string name,
        DateTime dueDate,
        string description = "",
        string type = "",
        string priority = "")
        => MutateAsync(
            ctx,
            rep,
            $"updateSalesRepTask(command: {{ id: \"{id}\", name: \"{name}\", dueDate: \"{Iso(dueDate)}\", " +
            $"description: \"{description}\", type: \"{type}\", priority: \"{priority}\" }}) " +
            "{ id name description type priority dueDate }");

    /// <summary>Straight through the task-management service, for the shapes this API cannot create.</summary>
    private static async Task SaveTaskDirectlyAsync(SalesRepTestContext ctx, Rep rep, string name, DateTime? dueDate, bool isActive, bool? completed)
    {
        var task = AbstractTypeFactory<WorkTask>.TryCreateInstance();
        task.Name = name;
        task.DueDate = dueDate;
        task.IsActive = isActive;
        task.Completed = completed;
        task.ResponsibleId = rep.MemberId;

        await ctx.GetRequiredService<IWorkTaskService>().SaveChangesAsync([task]);
    }

    private static Task<string[]> NamesForFilterAsync(SalesRepTestContext ctx, Rep rep, string filter) =>
        NamesForAsync(ctx, rep, $"filter: \"{filter}\"");

    private static async Task<string[]> NamesForAsync(SalesRepTestContext ctx, Rep rep, string arguments)
    {
        var json = await QueryAsync(ctx, rep, $"salesRepTasks({arguments}, today: \"{TodayIso}\") {{ items {{ name }} }}");

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
