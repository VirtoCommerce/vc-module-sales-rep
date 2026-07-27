using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class InputShareListWithCustomersType : ExtendableInputObjectGraphType<ShareListWithCustomersCommand>
{
    public InputShareListWithCustomersType()
    {
        Field<NonNullGraphType<StringGraphType>>(nameof(ShareListWithCustomersCommand.ListId))
            .Description("Wishlist (shopping list) to publish.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>(nameof(ShareListWithCustomersCommand.OrganizationIds))
            .Description("Customer organizations to share the list with. The Rep must serve each of them.");
        Field<StringGraphType>(nameof(ShareListWithCustomersCommand.Title))
            .Description("Optional notification title/heading.");
        Field<StringGraphType>(nameof(ShareListWithCustomersCommand.Message))
            .Description("Optional Rep message included in the email/push. The shared-list link is appended; the combined text must not exceed 1000 characters.");
        Field<NonNullGraphType<BooleanGraphType>>(nameof(ShareListWithCustomersCommand.SendPush))
            .Description("Send an in-store push notification to the customers' members.");
        Field<NonNullGraphType<BooleanGraphType>>(nameof(ShareListWithCustomersCommand.SendEmail))
            .Description("Send an email to the customers' members.");
        Field<NonNullGraphType<StringGraphType>>(nameof(ShareListWithCustomersCommand.StoreId))
            .Description("Store the list is published on behalf of (scopes the link host and the email template/sender).");
        Field<StringGraphType>(nameof(ShareListWithCustomersCommand.CultureName))
            .Description("Optional culture for localizing the notification (e.g. \"en-US\").");
    }
}
