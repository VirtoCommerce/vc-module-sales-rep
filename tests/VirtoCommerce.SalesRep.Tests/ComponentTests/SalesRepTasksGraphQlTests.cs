using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
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
    public async Task Mutations_OnAnotherRepsTask_AreRefused_AndChangeNothing()
    {
        using var ctx = SalesRepTestContext.Create();
        var ann = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);
        var bob = await SeedRepAsync(ctx, "Bob", "Rep", "bob@test.com", OrgA);

        var bobTask = await CreateTaskAsync(ctx, bob, "Bob private task", Today);
        var bobTaskId = bobTask.GetProperty("id").GetString();

        var mutations = new[]
        {
            $"updateSalesRepTask(command: {{ id: \"{bobTaskId}\", name: \"Hijacked\", description: \"\", type: \"\", priority: \"\", dueDate: \"{TodayIso}\" }}) {{ id name }}",
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
    public async Task Mutations_AnswerTheSameForANotFoundIdAndSomeoneElsesTask()
    {
        using var ctx = SalesRepTestContext.Create();
        var ann = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);
        var bob = await SeedRepAsync(ctx, "Bob", "Rep", "bob@test.com", OrgA);

        var bobTaskId = (await CreateTaskAsync(ctx, bob, "Bob private task", Today)).GetProperty("id").GetString();

        // A write must not become an existence oracle: probing a real id the caller does not own has to read
        // exactly like probing one that was never issued, the way salesRepTask already answers null for both.
        foreach (var mutation in new[]
        {
            "updateSalesRepTask(command: {{ id: \"{0}\", name: \"X\", dueDate: \"" + TodayIso + "\", description: \"\", type: \"\", priority: \"\" }}) {{ id }}",
            "changeSalesRepTaskStatus(command: {{ id: \"{0}\", completed: true }}) {{ id }}",
            "deleteSalesRepTask(command: {{ id: \"{0}\" }})",
        })
        {
            var onOthers = await MutateAsync(ctx, ann, string.Format(mutation, bobTaskId));
            var onMissing = await MutateAsync(ctx, ann, string.Format(mutation, "00000000-0000-0000-0000-0000000000ff"));

            onOthers.Should().Contain("\"errors\"");
            onOthers.Should().Contain("Task not found.");
            onOthers.Should().NotContain("Bob private task");

            // Same message, so the difference itself carries no information.
            onMissing.Should().Contain("Task not found.");
        }
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

        // Rule vocabulary is rep-only too - both kinds, which reach the scope check by different paths - and so is
        // the type dictionary. Every list-shaped surface, so none of them can regress unnoticed.
        foreach (var list in new[] { "salesRepTaskFilterRules { name }", "salesRepTaskSortRules { name }", "salesRepTaskTypes" })
        {
            SalesRepTestContext.Node(
                await ctx.ExecuteGraphQlAsync($"query {{ {list} }}", userId: outsiderUserId, memberId: outsiderContact.Id),
                list.Split(' ')[0]).GetArrayLength().Should().Be(0);
        }

        // And by id: a non-rep gets the same null a rep gets for someone else's task.
        var annTaskId = SalesRepTestContext.Node(
            await QueryAsync(ctx, rep, "salesRepTasks { items { id } }"), "salesRepTasks")
            .GetProperty("items")[0].GetProperty("id").GetString();

        SalesRepTestContext.Node(
            await ctx.ExecuteGraphQlAsync($"query {{ salesRepTask(id: \"{annTaskId}\") {{ id name }} }}", userId: outsiderUserId, memberId: outsiderContact.Id),
            "salesRepTask").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

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
        var canceledId = await SaveTaskDirectlyAsync(ctx, rep, "Canceled", dueDate: Today.AddDays(1), isActive: false, completed: false);
        // Closed-without-completing has a second shape: TimeoutAsync sets IsActive only, leaving Completed null.
        var timedOutId = await SaveTaskDirectlyAsync(ctx, rep, "Timed out", dueDate: Today.AddDays(1), isActive: false, completed: null);

        // The upstream criteria bound the due date with >= / <=, which drop NULLs, and `completed` means finished as
        // done - so neither row matches any rule. Pinned, because it means the tab counts do NOT sum to the total:
        // making them sum needs a "no due date" flag on WorkTaskSearchCriteria upstream, not a change here.
        (await NamesForFilterAsync(ctx, rep, "upcoming")).Should().Equal("Has a due date");
        (await NamesForFilterAsync(ctx, rep, "overdue")).Should().BeEmpty();
        (await NamesForFilterAsync(ctx, rep, "completed")).Should().BeEmpty();

        // They are still the rep's tasks, so the unfiltered list keeps them visible rather than hiding work.
        Names(await ListTasksAsync(ctx, rep)).Should().BeEquivalentTo("Has a due date", "No due date", "Canceled", "Timed out");

        // ...and the storefront renders that row with a live toggle, so the mutation is reachable. Both directions
        // are refused: raising IsActive would turn a cancellation into an ordinary open task with no way back, and
        // completing it would silently promote a cancellation to a completion.
        // Both shapes and both directions - so narrowing the guard to `Completed == false` cannot pass this test.
        foreach (var id in new[] { canceledId, timedOutId })
        {
            foreach (var completed in new[] { "true", "false" })
            {
                var refused = await MutateAsync(ctx, rep, $"changeSalesRepTaskStatus(command: {{ id: \"{id}\", completed: {completed} }}) {{ id }}");
                refused.Should().Contain("\"errors\"");
            }
        }

        var stored = (await ListTasksAsync(ctx, rep)).GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == "Canceled");
        stored.GetProperty("isActive").GetBoolean().Should().BeFalse();
        stored.GetProperty("completed").GetBoolean().Should().BeFalse();
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

        // The schema must not differ per deployment (the frontend generates types against a live endpoint), so every
        // surface answers rather than disappears. All nine, because a gate is only worth what it covers: the two rule
        // vocabularies reach the storage check by a different path from the three data reads and would otherwise
        // render furnished, zero-badged tabs over a list that can never return a row.
        (await ListTasksAsync(ctx, rep)).GetProperty("totalCount").GetInt32().Should().Be(0);

        SalesRepTestContext.Node(
            await QueryAsync(ctx, rep, "salesRepTask(id: \"any-id\") { id }"),
            "salesRepTask").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        foreach (var list in new[] { "salesRepTaskFilterRules { name }", "salesRepTaskSortRules { name }", "salesRepTaskTypes" })
        {
            SalesRepTestContext.Node(
                await QueryAsync(ctx, rep, list), list.Split(' ')[0]).GetArrayLength().Should().Be(0);
        }

        // And every write refuses with the same message rather than throwing.
        foreach (var mutation in new[]
        {
            $"createSalesRepTask(command: {{ name: \"X\", dueDate: \"{TodayIso}\" }}) {{ id }}",
            $"updateSalesRepTask(command: {{ id: \"any-id\", name: \"X\", description: \"\", type: \"\", priority: \"\", dueDate: \"{TodayIso}\" }}) {{ id }}",
            "changeSalesRepTaskStatus(command: { id: \"any-id\", completed: true }) { id }",
            "deleteSalesRepTask(command: { id: \"any-id\" })",
        })
        {
            var json = await MutateAsync(ctx, rep, mutation);

            json.Should().Contain("\"errors\"");
            json.Should().MatchRegex("(?i)not available");
        }
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
    public async Task DayBoundaryAtTheFloor_IsClamped_NotAnUnhandledException()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        await CreateTaskAsync(ctx, rep, "Due tomorrow", Today.AddDays(1));

        // `today` is unguarded client input. At DateTime.MinValue the overdue epsilon would underflow into an
        // ArgumentOutOfRangeException - which is not an ExecutionError, so x-api strips its message in production
        // and logs it as a crash. Clamped instead: nothing can be due before the floor, so the tab is simply empty.
        var json = await QueryAsync(ctx, rep,
            "salesRepTasks(filter: \"overdue\", today: \"0001-01-01T00:00:00Z\") { totalCount items { name } }");

        json.Should().NotContain("\"errors\"");
        SalesRepTestContext.Node(json, "salesRepTasks").GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData("Urgent")]
    // Enum.TryParse accepts two shapes that are not priority names. Numeric strings, in or out of range:
    [InlineData("999")]
    [InlineData("-1")]
    [InlineData("3")]
    // ...and a comma-separated list, for ANY enum rather than only a [Flags] one, ORing the members - so this
    // one is Low(1) | Normal(2) = 3 = High, a typo silently carried through as a different priority.
    [InlineData("Low, Normal")]
    public async Task UnknownPriority_IsRejected_RatherThanSilentlyDefaulted(string priority)
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        // Strict on purpose: the module's own SafeParse would quietly store Normal and the rep would never know.
        var json = await MutateAsync(ctx, rep, $"createSalesRepTask(command: {{ name: \"Typo\", dueDate: \"{TodayIso}\", priority: \"{priority}\" }}) {{ id priority }}");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)priority");

        (await ListTasksAsync(ctx, rep)).GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData("blank", "name")]
    [InlineData("long-name", "name")]
    [InlineData("long-type", "type")]
    // The storage columns cap Name at 256 and Type at 128 and nothing downstream checks them, so without this guard
    // an over-long value reaches SaveChangesAsync as a DbUpdateException instead of an error the caller can read.
    // Worth pinning precisely because the harness cannot catch the underlying failure: SQLite ignores VARCHAR length.
    public async Task WriteInputs_RejectBlankAndOverlongFields(string shape, string expectedInMessage)
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        var (name, type) = shape switch
        {
            "blank" => ("   ", ""),
            "long-name" => (new string('n', ModuleConstants.Tasks.MaxNameLength + 1), ""),
            _ => ("Fine", new string('t', ModuleConstants.Tasks.MaxTypeLength + 1)),
        };

        var json = await MutateAsync(ctx, rep,
            $"createSalesRepTask(command: {{ name: \"{name}\", type: \"{type}\", dueDate: \"{TodayIso}\" }}) {{ id }}");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex($"(?i){expectedInMessage}");
        (await ListTasksAsync(ctx, rep)).GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task WriteInputs_AcceptTheLimitExactly_AndUpdateIsGuardedByTheSamePath()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        // The cap is inclusive, and it is measured on the TRIMMED value - the same one ApplyInput stores.
        var atLimit = new string('n', ModuleConstants.Tasks.MaxNameLength);
        var created = SalesRepTestContext.Node(
            await MutateAsync(ctx, rep, $"createSalesRepTask(command: {{ name: \" {atLimit} \", dueDate: \"{TodayIso}\" }}) {{ id name }}"),
            "createSalesRepTask");
        created.GetProperty("name").GetString().Should().Be(atLimit);
        var taskId = created.GetProperty("id").GetString();

        // Validation lives in the shared ApplyInput, so update is guarded by the same code - pinned here so a future
        // mutation that skips ApplyInput cannot quietly lose it.
        var refused = await MutateAsync(ctx, rep,
            $"updateSalesRepTask(command: {{ id: \"{taskId}\", name: \"  \", description: \"\", type: \"\", priority: \"\", dueDate: \"{TodayIso}\" }}) {{ id }}");

        refused.Should().Contain("\"errors\"");
        refused.Should().MatchRegex("(?i)name");

        var stored = await ctx.GetRequiredService<IWorkTaskService>().GetByIdAsync(taskId);
        stored.Name.Should().Be(atLimit);
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
    public async Task SortRules_PublishTheWholeVocabulary_AndEachOneResolves()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        await CreateTaskAsync(ctx, rep, "Beta", Today.AddDays(2));
        await CreateTaskAsync(ctx, rep, "Alpha", Today.AddDays(1));

        var rules = SalesRepTestContext.Node(await QueryAsync(ctx, rep, "salesRepTaskSortRules { name }"), "salesRepTaskSortRules")
            .EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToArray();
        rules.Should().Equal("due-date", "recent", "name");

        // Every published rule has to be usable, not just offered: `name` had no test at all, and `recent` was
        // exercised only in the direction it REFUSES.
        Names(await ListTasksAsync(ctx, rep, "sort: \"name\"")).Should().Equal("Alpha", "Beta");
        Names(await ListTasksAsync(ctx, rep, "sort: \"name:desc\"")).Should().Equal("Beta", "Alpha");

        // `recent` orders by CreatedDate, which two rows written in the same tick can share - so this pins that the
        // valid direction is ACCEPTED and complete, and leaves the ordering to the due-date rules above.
        Names(await ListTasksAsync(ctx, rep, "sort: \"recent\"")).Should().BeEquivalentTo("Alpha", "Beta");
    }

    // The ordering itself is asserted on the resolved criteria rather than end to end: two rows written in the
    // same tick share a ModifiedDate, so a list-order assertion would pass or fail on clock resolution.
    // WorkTaskSearchService.BuildSortExpression hands criteria.SortInfos straight to the query, so what this
    // pins is the whole of our contribution.
    [Theory]
    [InlineData("due-date", "dueDate:asc;modifiedDate:desc")]
    [InlineData("due-date:desc", "dueDate:desc;modifiedDate:desc")]
    [InlineData("name", "name:asc;modifiedDate:desc")]
    // `recent` already orders by a timestamp, but CreatedDate is not ModifiedDate - an edited task still has
    // to float inside a group created together.
    [InlineData("recent", "createdDate:desc;modifiedDate:desc")]
    // An unknown rule falls back to the first one, and the tie-break rides along with it.
    [InlineData("no-such-rule", "dueDate:asc;modifiedDate:desc")]
    public async Task SortRules_EndWithARecencyTieBreak(string sort, string expected)
    {
        var criteria = await new SalesRepTaskSortRuleResolver()
            .ApplySortAsync(storeId: null, sort, new WorkTaskSearchCriteria());

        criteria.Sort.Should().Be(expected);
    }

    [Fact]
    public async Task Paging_ExposesCursorMetadata()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        await CreateTaskAsync(ctx, rep, "First", Today.AddDays(1));
        await CreateTaskAsync(ctx, rep, "Second", Today.AddDays(2));
        await CreateTaskAsync(ctx, rep, "Third", Today.AddDays(3));

        var page = SalesRepTestContext.Node(
            await QueryAsync(ctx, rep, "salesRepTasks(first: 2) { totalCount pageInfo { hasNextPage endCursor } items { name } }"),
            "salesRepTasks");

        page.GetProperty("totalCount").GetInt32().Should().Be(3);
        page.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean().Should().BeTrue();
        page.GetProperty("pageInfo").GetProperty("endCursor").GetString().Should().Be("2");

        var last = SalesRepTestContext.Node(
            await QueryAsync(ctx, rep, "salesRepTasks(first: 2, after: \"2\") { pageInfo { hasNextPage } items { name } }"),
            "salesRepTasks");
        last.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean().Should().BeFalse();
        Names(last).Should().Equal("Third");
    }

    [Fact]
    public async Task DayBoundary_FallsBackToTheCurrentUtcDay_WhenTodayIsOmitted()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        // Every other filter test passes `today` explicitly, so ResolveDayStart's fallback never ran. Real UTC dates
        // here rather than the suite's fixed Today, because the fallback is DateTime.UtcNow.Date by definition.
        var utcToday = DateTime.UtcNow.Date;
        await CreateTaskAsync(ctx, rep, "Yesterday", utcToday.AddDays(-1));
        await CreateTaskAsync(ctx, rep, "Tomorrow", utcToday.AddDays(1));

        // NOT NamesForFilterAsync - that helper always injects `today`, which is exactly what must be absent here.
        Names(await ListTasksAsync(ctx, rep, "filter: \"overdue\"")).Should().Equal("Yesterday");
        Names(await ListTasksAsync(ctx, rep, "filter: \"upcoming\"")).Should().Equal("Tomorrow");
    }

    [Fact]
    public async Task DatelessTask_CanBeWrittenBackUnchanged()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        // Arrives from the admin UI or a workflow - createSalesRepTask refuses to make one (see below).
        var taskId = await SaveTaskDirectlyAsync(ctx, rep, "No due date", dueDate: null, isActive: true, completed: null);

        // The whole point of matching the input shape to the read: a client reads dueDate: null and can send it back.
        // While dueDate was DateTime! this task was visible in the rep's list and impossible to edit at all.
        var updated = SalesRepTestContext.Node(
            await MutateAsync(ctx, rep, $"updateSalesRepTask(command: {{ id: \"{taskId}\", name: \"Renamed\", dueDate: null }}) {{ name dueDate }}"),
            "updateSalesRepTask");

        updated.GetProperty("name").GetString().Should().Be("Renamed");
        updated.GetProperty("dueDate").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task CreateSalesRepTask_StillRequiresADueDate_EvenThoughTheTypeAllowsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        // The rule moved from the type system into validation when the inputs were aligned with the read. It is a
        // product rule - a dateless task lands in no tab and on no calendar day - so it says so, rather than failing
        // at input coercion. Update deliberately does NOT share it.
        foreach (var dueDate in new[] { "null", null })
        {
            var argument = dueDate == null ? string.Empty : $", dueDate: {dueDate}";
            var json = await MutateAsync(ctx, rep, $"createSalesRepTask(command: {{ name: \"X\"{argument} }}) {{ id }}");

            json.Should().Contain("\"errors\"");
            json.Should().MatchRegex("(?i)due date");
        }

        (await ListTasksAsync(ctx, rep)).GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task UpdateSalesRepTask_ReplacesEveryEditableField_AndTreatsNullOmittedAndEmptyAlike()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        var created = await CreateTaskAsync(ctx, rep, "Draft", Today, priority: "High", type: "Call", description: "First pass.");
        var taskId = created.GetProperty("id").GetString();

        // REPLACES, not patches - so an omitted field is CLEARED. The inputs mirror what salesRepTask returns rather
        // than forcing non-null, because a client has to be able to write back exactly what it just read; the cost is
        // that "omitted" and "cleared" are one instruction, which is what replace semantics mean.
        var omitted = SalesRepTestContext.Node(
            await MutateAsync(ctx, rep, $"updateSalesRepTask(command: {{ id: \"{taskId}\", name: \"Renamed\", dueDate: \"{TodayIso}\" }}) {{ name description type priority }}"),
            "updateSalesRepTask");
        omitted.GetProperty("name").GetString().Should().Be("Renamed");
        omitted.GetProperty("description").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        omitted.GetProperty("type").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        omitted.GetProperty("priority").GetString().Should().Be("Normal");

        // An explicit null says the same thing, and null is precisely what the read returns for a cleared field.
        var nulls = SalesRepTestContext.Node(
            await MutateAsync(ctx, rep, $"updateSalesRepTask(command: {{ id: \"{taskId}\", name: \"Renamed\", dueDate: \"{TodayIso}\", description: null, type: null, priority: null }}) {{ description type priority }}"),
            "updateSalesRepTask");
        nulls.GetProperty("description").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        nulls.GetProperty("type").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        nulls.GetProperty("priority").GetString().Should().Be("Normal");

        // Restore the values so the explicit-clear leg below still has something to clear.
        await UpdateTaskAsync(ctx, rep, taskId, "Draft", Today, description: "First pass.", type: "Call", priority: "High");

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

    [Fact]
    public async Task Writes_TolerateACaseVariantOfTheCallersOwnMemberId()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "Ann", "Rep", "ann@test.com", OrgA);

        // A case variant of the caller's OWN member id is still their id, so a write must not refuse it - that
        // would lock a rep out of their own task. It reaches nobody else: another rep's id is a different GUID.
        var taskId = await SaveTaskDirectlyAsync(ctx, rep, "Case-shifted owner", Today, isActive: true,
            completed: null, responsibleId: rep.MemberId.ToUpperInvariant());

        var updated = SalesRepTestContext.Node(
            await MutateAsync(
                ctx,
                rep,
                $"updateSalesRepTask(command: {{ id: \"{taskId}\", name: \"Renamed\", dueDate: \"{TodayIso}\", description: \"\", type: \"\", priority: \"\" }}) {{ name }}"),
            "updateSalesRepTask");

        updated.GetProperty("name").GetString().Should().Be("Renamed");

        // Deliberately no assertion on whether the LIST shows it: that follows the column's collation, which is
        // the harness's (SQLite) here and changes under us when CI collations land. Only the convention is pinned.
    }

    [Fact]
    public async Task CreateSalesRepTask_LeavesTheStoreUnsetForAnAccountWithNoStore()
    {
        using var ctx = SalesRepTestContext.Create();
        // Asked for explicitly: SeedRepAsync binds the account to the default store, as a real one is.
        var rep = await SeedRepInStoreAsync(ctx, "Ann", "Rep", "ann@test.com", storeId: null, OrgA);

        // A rep whose account is not store-bound is supported configuration (SalesRepDetails.StoreId is
        // optional), so the write succeeds and the task simply carries no store.
        await CreateTaskAsync(ctx, rep, "Storeless", Today);

        Names(await ListTasksAsync(ctx, rep)).Should().Equal("Storeless");

        // The documented consequence: a storeId-filtered read cannot return it. The storefront never sends one.
        Names(await ListTasksAsync(ctx, rep, "storeId: \"B2B-store\"")).Should().BeEmpty();
    }

    // -- helpers -------------------------------------------------------------------------------------------------

    private sealed record Rep(string UserId, string MemberId);

    private static string Iso(DateTime value) => value.ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static string[] Names(System.Text.Json.JsonElement node) =>
        node.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToArray();

    private static Task<Rep> SeedRepAsync(SalesRepTestContext ctx, string firstName, string lastName, string email, params string[] organizationIds)
        => SeedRepInStoreAsync(ctx, firstName, lastName, email, SalesRepTestContext.DefaultStoreId, organizationIds);

    private static async Task<Rep> SeedRepInStoreAsync(SalesRepTestContext ctx, string firstName, string lastName, string email, string storeId, params string[] organizationIds)
    {
        await ctx.SeedOrganizationsAsync(organizationIds);
        var details = await ctx.CreateRepInStoreAsync(firstName, lastName, email, storeId, organizationIds);

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
    private static async Task<string> SaveTaskDirectlyAsync(
        SalesRepTestContext ctx,
        Rep rep,
        string name,
        DateTime? dueDate,
        bool isActive,
        bool? completed,
        string responsibleId = null)
    {
        var task = AbstractTypeFactory<WorkTask>.TryCreateInstance();
        task.Name = name;
        task.DueDate = dueDate;
        task.IsActive = isActive;
        task.Completed = completed;
        task.ResponsibleId = responsibleId ?? rep.MemberId;

        await ctx.GetRequiredService<IWorkTaskService>().SaveChangesAsync([task]);

        return task.Id;
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
