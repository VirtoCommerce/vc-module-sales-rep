using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.PushMessages.Core.Models;
using VirtoCommerce.SalesRep.Core.Notifications;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// Component tests for the <c>sendCustomerCommunication</c> mutation (VCST-5310 / VCST-5331): real recipient
/// resolution over the real member search + real security scoping, with capturing doubles for the two external
/// delivery services. Asserts the push and email channels are fed from the SAME resolved audience, attempt
/// independently, and report their outcome (per-channel booleans + stable warning codes).
/// </summary>
[Trait("Category", "Component")]
public class SalesRepCommunicationComponentTests
{
    private const string Store = "B2B-store";
    private const string StoreEmail = "no-reply@b2b.test";

    private static string Mutation(string organizationId, bool push, bool email, string message = "Hello there", string title = "Update", string storeId = Store)
        => $$"""
            mutation {
              sendCustomerCommunication(command: {
                organizationId: "{{organizationId}}",
                sendPush: {{(push ? "true" : "false")}},
                sendEmail: {{(email ? "true" : "false")}},
                title: "{{title}}",
                message: "{{message}}",
                storeId: "{{storeId}}"
              }) {
                succeeded
                pushSent
                emailSent
                warnings
              }
            }
            """;

    private static TestGraphQlConfiguration.CapturingPushMessageService Push(SalesRepTestContext ctx)
        => ctx.GetRequiredService<TestGraphQlConfiguration.CapturingPushMessageService>();

    private static TestGraphQlConfiguration.CapturingNotificationSender Email(SalesRepTestContext ctx)
        => ctx.GetRequiredService<TestGraphQlConfiguration.CapturingNotificationSender>();

    private static TestGraphQlConfiguration.StubNotificationSearchService EmailTemplates(SalesRepTestContext ctx)
        => ctx.GetRequiredService<TestGraphQlConfiguration.StubNotificationSearchService>();

    /// <summary>Seed a customer org with two contacts (each with an email), served by a rep bound to a store that has a sender address.</summary>
    private static async Task<string> SeedServedOrgWithContactsAsync(SalesRepTestContext ctx)
    {
        ctx.SetStoreEmail(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; c.FirstName = "Cee"; c.LastName = "One"; });
        await ctx.SeedContactAsync("c2", c => { c.Organizations = ["org-1"]; c.Emails = ["c2@test.com"]; c.FirstName = "Cee"; c.LastName = "Two"; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");
        return rep.UserId;
    }

    [Fact]
    public async Task SendCommunication_BothChannels_DeliverToSameRecipients()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true, message: "See https://x.test/list"), userId: repUserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":true");
        json.Should().Contain("\"warnings\":[]");

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
        emails.Should().OnlyContain(x => x.From == StoreEmail);

        // Same audience: both channels cover the same number of recipients (each seeded member has one email).
        Email(ctx).Scheduled.Should().HaveCount(pushMessage.MemberIds.Count);
    }

    [Fact]
    public async Task SendCommunication_PrimaryContactResolver_DeliversOnlyToPrimaryContact()
    {
        // The recipient audience is a DI-selected strategy; the default (and all other tests here) is AllMembers.
        // A project can register the PrimaryContactRecipientResolver instead — swap it in (plus its dependency) and
        // assert the message reaches ONLY the organization's primary contact, not every member.
        using var ctx = SalesRepTestContext.Create(services =>
        {
            services.AddTransient<ISalesRepPrimaryContactResolver, SalesRepPrimaryContactResolver>();
            services.AddTransient<ISalesRepRecipientResolver, PrimaryContactRecipientResolver>();
        });
        ctx.SetStoreEmail(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1", o => o.OwnerId = "c1"); // c1 is the org's primary contact (its owner)
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; c.FirstName = "Primary"; c.LastName = "Contact"; });
        await ctx.SeedContactAsync("c2", c => { c.Organizations = ["org-1"]; c.Emails = ["c2@test.com"]; c.FirstName = "Other"; c.LastName = "Member"; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":true");

        // Only the primary contact is addressed on both channels — the other member (c2) is excluded, unlike AllMembers.
        Push(ctx).Saved.Single().MemberIds.Should().Equal("c1");
        Email(ctx).Scheduled.OfType<SalesRepMessageEmailNotification>().Select(x => x.To).Should().Equal("c1@test.com");
    }

    [Fact]
    public async Task SendCommunication_PushOnly_DoesNotSendEmail()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: false), userId: repUserId);

        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[]");
        Push(ctx).Saved.Should().HaveCount(1);
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_EmailOnly_DoesNotCreatePush()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: repUserId);

        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":false").And.Contain("\"emailSent\":true");
        json.Should().Contain("\"warnings\":[]");
        Push(ctx).Saved.Should().BeEmpty();
        Email(ctx).Scheduled.OfType<SalesRepMessageEmailNotification>().Select(x => x.To).Should().Contain(["c1@test.com", "c2@test.com"]);
    }

    [Fact]
    public async Task SendCommunication_RepDoesNotServeOrganization_ReturnsForbiddenAndSendsNothing()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        // Rep serves org-2, not org-1.
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-2");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: rep.UserId);

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)access denied");
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
    public async Task SendCommunication_TitleTooLong_ReturnsValidationError()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(
            Mutation("org-1", push: true, email: false, title: new string('t', 129)),
            userId: repUserId);

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)128");
        Push(ctx).Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_EmailChannelThrows_DoesNotAbortMutation_PushStillDispatched()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);
        Email(ctx).ThrowOnSchedule = true; // email delivery fails (e.g. sender queue error)

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: repUserId);

        // A channel failure must NOT surface as a GraphQL error; push still succeeds → overall success + a warning.
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[\"EmailSendFailed\"]");
        Push(ctx).Saved.Should().HaveCount(1);
    }

    [Fact]
    public async Task SendCommunication_OnlyEmailChannelThrows_ReturnsFailedWarningWithoutError()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);
        Email(ctx).ThrowOnSchedule = true;

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: repUserId);

        // The only selected channel failed → not succeeded, but no unhandled error bubbles to the client.
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":false").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[\"EmailSendFailed\"]");
    }

    [Fact]
    public async Task SendCommunication_ExcludesInitiatingRep()
    {
        using var ctx = SalesRepTestContext.Create();
        ctx.SetStoreEmail(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        // The rep serves org-1, so their own contact is a member of it — the rep must NOT receive their own send.
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true");

        // Push: the customer contact is addressed; the initiating rep's own member id is excluded.
        var pushMessage = Push(ctx).Saved.Single();
        pushMessage.MemberIds.Should().Contain("c1").And.NotContain(rep.Id);

        // Email: the customer's address is used; the rep's own address is excluded.
        var emails = Email(ctx).Scheduled.OfType<SalesRepMessageEmailNotification>().Select(x => x.To).ToList();
        emails.Should().Contain("c1@test.com").And.NotContain("jane@test.com");
    }

    [Fact]
    public async Task SendCommunication_EmailOnly_TemplateMissing_ReturnsEmailUnavailable()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);
        EmailTemplates(ctx).TemplateAvailable = false; // store has no email template configured

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: repUserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":false").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[\"EmailUnavailable\"]");
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_TemplateMissing_PushStillSends()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);
        EmailTemplates(ctx).TemplateAvailable = false;

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: repUserId);

        // A missing email template only skips email — push is independent and still delivers.
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[\"EmailUnavailable\"]");
        Push(ctx).Saved.Should().HaveCount(1);
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_StoreHasNoSender_ReturnsEmailUnavailable()
    {
        using var ctx = SalesRepTestContext.Create();
        // Store resolves (via ContactDefaultStatus) and IS the caller's store, but has no sender From address.
        ctx.SetStoreContactDefaultStatus(Store, "Approved");
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":false");
        json.Should().Contain("\"warnings\":[\"EmailUnavailable\"]");
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_EmailOnly_NoRecipientAddresses_ReturnsEmailNoRecipients()
    {
        using var ctx = SalesRepTestContext.Create();
        ctx.SetStoreEmail(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1");
        // Contacts exist but none carries an email address.
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.FirstName = "Cee"; c.LastName = "One"; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":false");
        json.Should().Contain("\"warnings\":[\"EmailNoRecipients\"]");
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_PushAndEmail_NoRecipientAddresses_PushStillSent()
    {
        using var ctx = SalesRepTestContext.Create();
        ctx.SetStoreEmail(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.FirstName = "Cee"; c.LastName = "One"; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: rep.UserId);

        // The audience is reachable by push, just not by email — push is delivered and the call succeeds overall.
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[\"EmailNoRecipients\"]");
        Push(ctx).Saved.Should().HaveCount(1);
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_NoRecipients_ReturnsNoRecipientsWarning()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationAsync("org-1");
        // The org has no members other than the rep, who is excluded as the initiator — no one is left to receive.
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":false");
        json.Should().Contain("\"warnings\":[\"NoRecipients\"]");
        Push(ctx).Saved.Should().BeEmpty();
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_EmailOnly_ForeignStore_ReturnsEmailStoreAccessDenied()
    {
        using var ctx = SalesRepTestContext.Create();
        ctx.SetStoreEmail(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        // The rep is bound to B2B-store but passes a different store — email is scoped to the caller's store.
        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true, storeId: "OtherStore"), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":false");
        json.Should().Contain("\"warnings\":[\"EmailStoreAccessDenied\"]");
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_ForeignStore_EmailDeniedButPushStillSends()
    {
        using var ctx = SalesRepTestContext.Create();
        ctx.SetStoreEmail(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true, storeId: "OtherStore"), userId: rep.UserId);

        // Store scoping only affects email; push is store-agnostic and still delivers.
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[\"EmailStoreAccessDenied\"]");
        Push(ctx).Saved.Should().HaveCount(1);
        Email(ctx).Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_TrustedGroupStore_EmailAllowed()
    {
        using var ctx = SalesRepTestContext.Create();
        // The rep's home store is "group-a"; the requested store trusts that group, so email is allowed.
        ctx.SetStoreEmail(Store, StoreEmail);
        ctx.SetStoreTrustedGroups(Store, "group-a");
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", "group-a", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"emailSent\":true");
        json.Should().Contain("\"warnings\":[]");
        Email(ctx).Scheduled.OfType<SalesRepMessageEmailNotification>().Select(x => x.To).Should().Contain("c1@test.com");
    }
}
