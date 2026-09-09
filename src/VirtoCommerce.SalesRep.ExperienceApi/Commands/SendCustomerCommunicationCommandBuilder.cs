using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class SendCustomerCommunicationCommandBuilder
    : SalesRepCommandBuilder<SendCustomerCommunicationCommand, SalesRepCommunicationResult, InputSendCustomerCommunicationType, SalesRepCommunicationResultType>
{
    protected override string Name => "sendCustomerCommunication";

    public SendCustomerCommunicationCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
