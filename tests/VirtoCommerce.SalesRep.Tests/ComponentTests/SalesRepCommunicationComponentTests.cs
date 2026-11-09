using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.NotificationsModule.Data.Caching;
using VirtoCommerce.NotificationsModule.Data.Model;
using VirtoCommerce.NotificationsModule.Data.Repositories;
using VirtoCommerce.PushMessages.Core.Models;
using VirtoCommerce.SalesRep.Core.Notifications;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// Component tests for the <c>sendCustomerCommunication</c> mutation (VCST-5310 / VCST-5331): real recipient
/// resolution over the real member search + real security scoping, delivered through the real push messages and
/// notifications modules: the push message is saved, the email rendered from the module's template and saved as a
/// message. Asserts the push and email channels are fed from the SAME resolved audience, attempt independently, and
/// report their outcome (per-channel booleans + stable warning codes).
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

    /// <summary>The VCST-5850 shape: several organizations at once, optionally alongside the legacy single organizationId.</summary>
    private static string MultiOrgMutation(string[] organizationIds, string legacyOrganizationId = null, bool push = true, bool email = true)
    {
        var legacy = legacyOrganizationId == null ? "" : $"organizationId: \"{legacyOrganizationId}\",";
        var ids = string.Join(", ", organizationIds.Select(x => $"\"{x}\""));

        return $$"""
            mutation {
              sendCustomerCommunication(command: {
                {{legacy}}
                organizationIds: [{{ids}}],
                sendPush: {{(push ? "true" : "false")}},
                sendEmail: {{(email ? "true" : "false")}},
                title: "Update",
                message: "Hello there",
                storeId: "{{Store}}"
              }) {
                succeeded
                pushSent
                emailSent
                warnings
              }
            }
            """;
    }

    /// <summary>The push messages the push messages module saved (it fans them out to devices on its own).</summary>
    private static Task<IList<PushMessage>> PushMessagesAsync(SalesRepTestContext ctx) => ctx.GetPushMessagesAsync();

    /// <summary>
    /// The emails the notifications module rendered from the module's own template and saved as messages: this platform
    /// has no email gateway, so they stay there, and the sender is asked for nothing else.
    /// </summary>
    private static Task<IList<EmailNotificationMessage>> EmailsAsync(SalesRepTestContext ctx) => ctx.GetEmailMessagesAsync();

    /// <summary>
    /// The one way the real sender fails a send: saving the message, which it does after rendering it (a template that
    /// fails to render is saved too, with an error status, not thrown). With the messages' table gone, that write throws,
    /// as on a database that refuses it.
    /// </summary>
    private static async Task RefuseEmailMessagesAsync(SalesRepTestContext ctx)
    {
        await using var db = ctx.NewDbContext<NotificationDbContext>();
        var table = db.Model.FindEntityType(typeof(NotificationMessageEntity))!.GetTableName();

        var sql = "DROP TABLE \"" + table + "\"";

        await db.Database.ExecuteSqlRawAsync(sql);
    }

    /// <summary>
    /// The module's notification missing from the notifications module's database, where its searches read it (the
    /// notifications module saves every registered one at start): the rows go, and the caches the raw write bypasses.
    /// </summary>
    private static async Task RemoveEmailNotificationAsync(SalesRepTestContext ctx)
    {
        await using (var db = ctx.NewDbContext<NotificationDbContext>())
        {
            await db.Set<NotificationEntity>()
                .Where(x => x.Type == nameof(SalesRepMessageEmailNotification))
                .ExecuteDeleteAsync();
        }

        NotificationSearchCacheRegion.ExpireRegion();
        NotificationCacheRegion.ExpireRegion();
    }

    /// <summary>Seed a customer org with two contacts (each with an email), served by a rep bound to a store that has a sender address.</summary>
    private static async Task<string> SeedServedOrgWithContactsAsync(SalesRepTestContext ctx)
    {
        await ctx.SetStoreEmailAsync(Store, StoreEmail);
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
        (await PushMessagesAsync(ctx)).Should().HaveCount(1);
        var pushMessage = (await PushMessagesAsync(ctx))[0];
        pushMessage.Status.Should().Be(PushMessageStatus.Sent);
        pushMessage.ShortMessage.Should().Be("See https://x.test/list");
        pushMessage.MemberIds.Should().Contain(["c1", "c2"]);

        // Email: one per resolved member that has an address; content carried through.
        var emails = (await EmailsAsync(ctx)).ToList();
        emails.Should().OnlyContain(x => x.Subject == "Update" && x.Body.Contains("See https://x.test/list"));
        emails.Select(x => x.To).Should().Contain(["c1@test.com", "c2@test.com"]);
        emails.Should().OnlyContain(x => x.From == StoreEmail);

        // Same audience: both channels cover the same number of recipients (each seeded member has one email).
        (await EmailsAsync(ctx)).Should().HaveCount(pushMessage.MemberIds.Count);
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
        await ctx.SetStoreEmailAsync(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1", o => o.OwnerId = "c1"); // c1 is the org's primary contact (its owner)
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; c.FirstName = "Primary"; c.LastName = "Contact"; });
        await ctx.SeedContactAsync("c2", c => { c.Organizations = ["org-1"]; c.Emails = ["c2@test.com"]; c.FirstName = "Other"; c.LastName = "Member"; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":true");

        // Only the primary contact is addressed on both channels — the other member (c2) is excluded, unlike AllMembers.
        (await PushMessagesAsync(ctx)).Single().MemberIds.Should().Equal("c1");
        (await EmailsAsync(ctx)).Select(x => x.To).Should().Equal("c1@test.com");
    }

    [Fact]
    public async Task SendCommunication_PushOnly_DoesNotSendEmail()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: false), userId: repUserId);

        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[]");
        (await PushMessagesAsync(ctx)).Should().HaveCount(1);
        (await EmailsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_EmailOnly_DoesNotCreatePush()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: repUserId);

        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":false").And.Contain("\"emailSent\":true");
        json.Should().Contain("\"warnings\":[]");
        (await PushMessagesAsync(ctx)).Should().BeEmpty();
        (await EmailsAsync(ctx)).Select(x => x.To).Should().Contain(["c1@test.com", "c2@test.com"]);
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
        (await PushMessagesAsync(ctx)).Should().BeEmpty();
        (await EmailsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");

        var json = await ctx.ExecuteGraphQlAnonymousAsync(Mutation("org-1", push: true, email: true));

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
        (await PushMessagesAsync(ctx)).Should().BeEmpty();
        (await EmailsAsync(ctx)).Should().BeEmpty();
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
        (await PushMessagesAsync(ctx)).Should().BeEmpty();
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
        (await PushMessagesAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_EmailChannelThrows_DoesNotAbortMutation_PushStillDispatched()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);
        await RefuseEmailMessagesAsync(ctx); // the notification store refuses the message: the real sender's only failure

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: repUserId);

        // A channel failure must NOT surface as a GraphQL error; push still succeeds → overall success + a warning.
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[\"EmailSendFailed\"]");
        (await PushMessagesAsync(ctx)).Should().HaveCount(1);
    }

    [Fact]
    public async Task SendCommunication_OnlyEmailChannelThrows_ReturnsFailedWarningWithoutError()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);
        await RefuseEmailMessagesAsync(ctx);

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
        await ctx.SetStoreEmailAsync(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        // The rep serves org-1, so their own contact is a member of it — the rep must NOT receive their own send.
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true");

        // Push: the customer contact is addressed; the initiating rep's own member id is excluded.
        var pushMessage = (await PushMessagesAsync(ctx)).Single();
        pushMessage.MemberIds.Should().Contain("c1").And.NotContain(rep.Id);

        // Email: the customer's address is used; the rep's own address is excluded.
        var emails = (await EmailsAsync(ctx)).Select(x => x.To).ToList();
        emails.Should().Contain("c1@test.com").And.NotContain("jane@test.com");
    }

    [Fact]
    public async Task SendCommunication_EmailOnly_TemplateMissing_ReturnsEmailUnavailable()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);
        await RemoveEmailNotificationAsync(ctx); // the notification is not in the notifications module's database

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: repUserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":false").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[\"EmailUnavailable\"]");
        (await EmailsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_TemplateMissing_PushStillSends()
    {
        using var ctx = SalesRepTestContext.Create();
        var repUserId = await SeedServedOrgWithContactsAsync(ctx);
        await RemoveEmailNotificationAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: repUserId);

        // A missing email template only skips email — push is independent and still delivers.
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[\"EmailUnavailable\"]");
        (await PushMessagesAsync(ctx)).Should().HaveCount(1);
        (await EmailsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_StoreHasNoSender_ReturnsEmailUnavailable()
    {
        using var ctx = SalesRepTestContext.Create();
        // Store resolves (via ContactDefaultStatus) and IS the caller's store, but has no sender From address.
        await ctx.SetStoreContactDefaultStatusAsync(Store, "Approved");
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":false");
        json.Should().Contain("\"warnings\":[\"EmailUnavailable\"]");
        (await EmailsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_EmailOnly_NoRecipientAddresses_ReturnsEmailNoRecipients()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SetStoreEmailAsync(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1");
        // Contacts exist but none carries an email address.
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.FirstName = "Cee"; c.LastName = "One"; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":false");
        json.Should().Contain("\"warnings\":[\"EmailNoRecipients\"]");
        (await EmailsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_PushAndEmail_NoRecipientAddresses_PushStillSent()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SetStoreEmailAsync(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.FirstName = "Cee"; c.LastName = "One"; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true), userId: rep.UserId);

        // The audience is reachable by push, just not by email — push is delivered and the call succeeds overall.
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[\"EmailNoRecipients\"]");
        (await PushMessagesAsync(ctx)).Should().HaveCount(1);
        (await EmailsAsync(ctx)).Should().BeEmpty();
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
        (await PushMessagesAsync(ctx)).Should().BeEmpty();
        (await EmailsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_EmailOnly_ForeignStore_ReturnsEmailStoreAccessDenied()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SetStoreEmailAsync(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        // The rep is bound to B2B-store but passes a different store — email is scoped to the caller's store.
        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true, storeId: "OtherStore"), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":false");
        json.Should().Contain("\"warnings\":[\"EmailStoreAccessDenied\"]");
        (await EmailsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_ForeignStore_EmailDeniedButPushStillSends()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SetStoreEmailAsync(Store, StoreEmail);
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: true, email: true, storeId: "OtherStore"), userId: rep.UserId);

        // Store scoping only affects email; push is store-agnostic and still delivers.
        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":false");
        json.Should().Contain("\"warnings\":[\"EmailStoreAccessDenied\"]");
        (await PushMessagesAsync(ctx)).Should().HaveCount(1);
        (await EmailsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_TrustedGroupStore_EmailAllowed()
    {
        using var ctx = SalesRepTestContext.Create();
        // The rep's home store is "group-a"; the requested store trusts that group, so email is allowed.
        await ctx.SetStoreEmailAsync(Store, StoreEmail);
        await ctx.SetStoreTrustedGroupsAsync(Store, "group-a");
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", "group-a", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"emailSent\":true");
        json.Should().Contain("\"warnings\":[]");
        (await EmailsAsync(ctx)).Select(x => x.To).Should().Contain("c1@test.com");
    }

    // Store ids are ids, and ids compare ignore-case in memory. Ordinal here refused a rep whose own store id
    // differs only in case from the one the store trusts: trusted groups are typed by an administrator. (The store's
    // own id in another case is no real input, since a rep's store is picked from the stores; on the platform such a
    // lookup also misses the case-sensitive database and caches that miss under the lowercased id, for every casing.)
    [Fact]
    public async Task SendCommunication_StoreIdDiffersOnlyInCase_EmailAllowed()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SetStoreEmailAsync(Store, StoreEmail);
        await ctx.SetStoreTrustedGroupsAsync(Store, "group-a");
        await ctx.SeedOrganizationAsync("org-1");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", "GROUP-A", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(Mutation("org-1", push: false, email: true), userId: rep.UserId);

        json.Should().Contain("\"emailSent\":true");
        json.Should().Contain("\"warnings\":[]");
    }

    [Fact]
    public async Task SendCommunication_MultipleOrganizations_DeliversOnceToEachMember()
    {
        // VCST-5850: one send to several served organizations; a contact in two of them gets the message once.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SetStoreEmailAsync(Store, StoreEmail);
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        await ctx.SeedContactAsync("c2", c => { c.Organizations = ["org-1", "org-2"]; c.Emails = ["c2@test.com"]; });
        await ctx.SeedContactAsync("c3", c => { c.Organizations = ["org-2"]; c.Emails = ["c3@test.com"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1", "org-2");

        var json = await ctx.ExecuteGraphQlAsync(MultiOrgMutation(["org-1", "org-2"]), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"succeeded\":true").And.Contain("\"pushSent\":true").And.Contain("\"emailSent\":true");
        (await PushMessagesAsync(ctx)).Should().ContainSingle().Which.MemberIds.Should().BeEquivalentTo("c1", "c2", "c3");
        (await EmailsAsync(ctx)).Select(x => x.To)
            .Should().BeEquivalentTo("c1@test.com", "c2@test.com", "c3@test.com");
    }

    [Fact]
    public async Task SendCommunication_LegacyOrganizationIdAndOrganizationIds_AreMerged()
    {
        // Released storefronts still send the single organizationId; it is one more recipient organization.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; });
        await ctx.SeedContactAsync("c3", c => { c.Organizations = ["org-2"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1", "org-2");

        var json = await ctx.ExecuteGraphQlAsync(MultiOrgMutation(["org-2"], legacyOrganizationId: "org-1", email: false), userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        (await PushMessagesAsync(ctx)).Should().ContainSingle().Which.MemberIds.Should().BeEquivalentTo("c1", "c3");
    }

    [Fact]
    public async Task SendCommunication_OneUnservedOrganization_ReturnsForbiddenAndSendsNothing()
    {
        // All-or-nothing: a single organization the rep does not serve rejects the whole send.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        await ctx.SeedContactAsync("c1", c => { c.Organizations = ["org-1"]; c.Emails = ["c1@test.com"]; });
        await ctx.SeedContactAsync("c3", c => { c.Organizations = ["org-2"]; c.Emails = ["c3@test.com"]; });
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(MultiOrgMutation(["org-1", "org-2"]), userId: rep.UserId);

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)access denied");
        (await PushMessagesAsync(ctx)).Should().BeEmpty();
        (await EmailsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_TooManyOrganizations_ReturnsValidationError()
    {
        // The cap is checked before the membership query and the recipient fan-out, so an oversized request costs
        // one validation instead of a per-organization resolve loop.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var organizationIds = Enumerable.Range(0, 1001).Select(x => $"org-{x}").ToArray();

        var json = await ctx.ExecuteGraphQlAsync(MultiOrgMutation(organizationIds), userId: rep.UserId);

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)1000");
        (await PushMessagesAsync(ctx)).Should().BeEmpty();
        (await EmailsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommunication_NoOrganization_ReturnsValidationError()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", Store, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(MultiOrgMutation([]), userId: rep.UserId);

        json.Should().Contain("\"errors\"");
        json.Should().Contain("Organization is required");
        (await PushMessagesAsync(ctx)).Should().BeEmpty();
    }
}
