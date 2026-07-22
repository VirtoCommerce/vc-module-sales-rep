using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.PushMessages.Core.Models;
using VirtoCommerce.SalesRep.Core.Notifications;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// Component tests for the <c>sendCustomerCommunication</c> mutation (VCST-5310 / VCST-5331): real recipient
/// resolution over the real member search + real security scoping, with capturing doubles for the two external
/// delivery services. Asserts the push and email channels are fed from the SAME resolved audience.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepCommunicationComponentTests
{
    private const string Store = "B2B-store";

    private static string Mutation(string organizationId, bool push, bool email, string message = "Hello there", string title = "Update")
        => $$"""
            mutation {
              sendCustomerCommunication(command: {
                organizationId: "{{organizationId}}",
                sendPush: {{(push ? "true" : "false")}},
                sendEmail: {{(email ? "true" : "false")}},
                title: "{{title}}",
                message: "{{message}}",
                storeId: "{{Store}}"
              })
            }
            """;

    private static TestGraphQlConfiguration.CapturingPushMessageService Push(SalesRepTestContext ctx)
        => ctx.GetRequiredService<TestGraphQlConfiguration.CapturingPushMessageService>();

    private static TestGraphQlConfiguration.CapturingNotificationSender Email(SalesRepTestContext ctx)
        => ctx.GetRequiredService<TestGraphQlConfiguration.CapturingNotificationSender>();

    /// <summary>Seed a customer org with two contacts (each with an email), served by a freshly-created rep.</summary>
    private static async Task<string> SeedServedOrgWithContactsAsync(SalesRepTestContext ctx)
    {
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; c.FirstName = "Cee"; c.LastName = "One"; });
        await ctx.SeedContactAsync("c2", c => { c.Organizations = ["org-1"]; c.Emails = ["c2@test.com"]; c.FirstName = "Cee"; c.LastName = "Two"; });
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        return rep.UserId;
    }

    [Fact]
    public async Task SendCommunication_BothChannels_DeliverToSameRecipients()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true, message: "See https://x.test/list"), userId: repUserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"sendCustomerCommunication\":true");

        // Push: exactly one message, addressed to member ids, carrying the text, marked Sent.
        Push(ctx).Saved.Should().HaveCount(1);
        var pushMessage = Push(ctx).Saved[0];
        pushMessage.Status.Should().Be(PushMessageStatus.Sent);
        pushMessage.ShortMessage.Should().Be("See https://x.test/list");
        pushMessage.MemberIds.Should().Contain(["c1", "c2"]);

        // Email: one per resolved member that has an address; content carried through.
        var emails = Email(ctx).Scheduled.OfType<SalesRepMessageEmailNotification>().ToList();
        emails.Should().OnlyContain(x => x.Message == "See https://x.test/list");
        emails.Select(x => x.To).Should().Contain(["c1@test.com", "c2@test.com"]);

        // Same audience: both channels cover the same number of recipients (each seeded member has one email).
        Email(ctx).Scheduled.Should().HaveCount(pushMessage.MemberIds.Count);
    }

    [Fact]
    public async Task SendCommunication_PushOnly_DoesNotSendEmail()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: false), userId: repUserId);

        json.Should().Contain("\"sendCustomerCommunication\":true");
        Push(ctx).Saved.Should().HaveCount(1);
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_EmailOnly_DoesNotCreatePush()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: repUserId);

        json.Should().Contain("\"sendCustomerCommunication\":true");
        Push(ctx).Saved.Should().BeEmpty();
        Email(ctx).Scheduled.OfType<SalesRepMessageEmailNotification>().Select(x => x.To).Should().Contain(["c1@test.com", "c2@test.com"]);
    }

    [Fact]
    public async Task SendCommunication_RepDoesNotServeOrganization_ReturnsFalseAndSendsNothing()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        // Rep serves org-2, not org-1.
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-2");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"sendCustomerCommunication\":false");
        Push(ctx).Saved.Should().BeEmpty();
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");

        var json = await ctx.ExecuteGraphQlAnonymousAsync(Mutation("org-1", push: true, email: true));

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
        Push(ctx).Saved.Should().BeEmpty();
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_MessageTooLong_ReturnsValidationError()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(
            Mutation("org-1", push: true, email: false, message: new string('a', 1001)),
            userId: repUserId);

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)1000");
        Push(ctx).Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_EmailChannelThrows_DoesNotAbortMutation_PushStillDispatched()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);
        Email(ctx).ThrowOnSchedule = true; // email delivery fails (e.g. unrenderable template)

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: repUserId);

        // A channel failure must NOT surface as a GraphQL error; push still succeeds → overall true.
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"sendCustomerCommunication\":true");
        Push(ctx).Saved.Should().HaveCount(1);
    }

    [Fact]
    public async Task SendCommunication_OnlyChannelThrows_ReturnsFalseWithoutError()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);
        Email(ctx).ThrowOnSchedule = true;

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: repUserId);

        // The only selected channel failed → false, but still no unhandled error bubbles to the client.
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"sendCustomerCommunication\":false");
    }

    [Fact]
    public async Task SendCommunication_ExcludesInitiatingRep()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        // The rep serves org-1, so their own contact is a member of it — the rep must NOT receive their own send.
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"sendCustomerCommunication\":true");

        // Push: the customer contact is addressed; the initiating rep's own member id is excluded.
        var pushMessage = Push(ctx).Saved.Single();
        pushMessage.MemberIds.Should().Contain("c1").And.NotContain(rep.Id);

        // Email: the customer's address is used; the rep's own address is excluded.
        var emails = Email(ctx).Scheduled.OfType<SalesRepMessageEmailNotification>().Select(x => x.To).ToList();
        emails.Should().Contain("c1@test.com").And.NotContain("jane@test.com");
    }
}
