using FluentAssertions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic tests for <see cref="SalesRepCommunicationResponseGroupParser"/> — the channels → recipient member
/// response-group mapping, so a push-only send doesn't hydrate member emails it never reads while email still gets
/// the Emails collection.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepCommunicationResponseGroupTests
{
    private static readonly ISalesRepCommunicationResponseGroupParser _parser = new SalesRepCommunicationResponseGroupParser();

    private static MemberResponseGroup Group(bool sendPush, bool sendEmail) =>
        EnumUtility.SafeParseFlags(
            _parser.GetResponseGroup(new SendCustomerCommunicationCommand { SendPush = sendPush, SendEmail = sendEmail }),
            MemberResponseGroup.Default);

    [Fact]
    public void PushOnly_LoadsDefaultOnly()
    {
        // Push addresses members by id — no email collection needed.
        Group(sendPush: true, sendEmail: false).Should().Be(MemberResponseGroup.Default);
    }

    [Fact]
    public void EmailOnly_RequestsWithEmails()
    {
        Group(sendPush: false, sendEmail: true).Should().Be(MemberResponseGroup.WithEmails);
    }

    [Fact]
    public void BothChannels_RequestsWithEmails()
    {
        // Email needs the Emails collection; push rides along on the same (superset) load.
        Group(sendPush: true, sendEmail: true).Should().Be(MemberResponseGroup.WithEmails);
    }
}
