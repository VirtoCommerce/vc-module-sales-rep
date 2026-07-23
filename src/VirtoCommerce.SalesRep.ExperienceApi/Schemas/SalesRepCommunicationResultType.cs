using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCommunicationResultType : ExtendableGraphType<SalesRepCommunicationResult>
{
    public SalesRepCommunicationResultType()
    {
        Name = "SalesRepCommunicationResult";

        Field(x => x.Succeeded, nullable: false)
            .Description("True when at least one requested channel was accepted for delivery.");
        Field(x => x.PushSent, nullable: false)
            .Description("Whether the push notification was accepted for delivery.");
        Field(x => x.EmailSent, nullable: false)
            .Description("Whether the email was accepted for delivery.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>(nameof(SalesRepCommunicationResult.Warnings))
            .Description("Stable outcome codes for any channel that did not deliver (empty on full success). The storefront maps each code to a localized message.")
            .Resolve(context => context.Source.Warnings);
    }
}
