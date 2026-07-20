using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepCommunicationResponseGroupParser : ISalesRepCommunicationResponseGroupParser
{
    public virtual string GetResponseGroup(SendCustomerCommunicationCommand command)
    {
        // Push addresses members by id only (Default); the email channel additionally needs the Emails collection.
        var result = MemberResponseGroup.Default;

        if (command.SendEmail)
        {
            result |= MemberResponseGroup.WithEmails;
        }

        return result.ToString();
    }
}
