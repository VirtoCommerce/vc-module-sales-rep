using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class InputSendCustomerCommunicationType : ExtendableInputObjectGraphType<SendCustomerCommunicationCommand>
{
    public InputSendCustomerCommunicationType()
    {
        Field<StringGraphType>(nameof(SendCustomerCommunicationCommand.OrganizationId))
            .Description("Customer organization whose members receive the message.")
            .DeprecationReason("Use organizationIds");
        Field<ListGraphType<NonNullGraphType<StringGraphType>>>(nameof(SendCustomerCommunicationCommand.OrganizationIds))
            .Description("Customer organizations whose members receive the message (each member once); at least one organization is required.");
        Field<NonNullGraphType<BooleanGraphType>>(nameof(SendCustomerCommunicationCommand.SendPush))
            .Description("Send an in-store push notification to the recipients.");
        Field<NonNullGraphType<BooleanGraphType>>(nameof(SendCustomerCommunicationCommand.SendEmail))
            .Description("Send an email to the recipients.");
        Field<StringGraphType>(nameof(SendCustomerCommunicationCommand.Title))
            .Description("Optional message title/heading.");
        Field<NonNullGraphType<StringGraphType>>(nameof(SendCustomerCommunicationCommand.Message))
            .Description("The Rep's message (required, max 1000 chars). May contain a URL.");
        Field<NonNullGraphType<StringGraphType>>(nameof(SendCustomerCommunicationCommand.StoreId))
            .Description("Store the message is sent on behalf of (scopes the email template and sender address).");
        Field<StringGraphType>(nameof(SendCustomerCommunicationCommand.CultureName))
            .Description("Optional culture for localizing the email template (e.g. \"en-US\").");
    }
}
