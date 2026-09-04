using System.Linq;
using FluentValidation;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Data.Validation;

// Runs on the Sales Rep aggregate save path (SalesRepService.SaveOneAsync) - the one place that writes the
// contact, the login account, the role and the organization memberships together. A rep is an ordinary Contact,
// so the platform's other contact writers can still produce a nameless one; covering them would mean fixing the
// non-null-over-nullable mismatch in the Experience API schema, which is deliberately out of scope here.
public class SalesRepDetailsValidator : AbstractValidator<SalesRepDetails>
{
    public SalesRepDetailsValidator()
    {
        // The storefront X-API publishes contact.firstName, .lastName and .fullName as NON-NULL GraphQL fields, so a
        // rep whose contact has no name breaks every query that reads the current user - including the sign-in page
        // context, which locks the rep out of the storefront entirely (VCST-5759). FullName is derived from these.
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(ModuleConstants.Profile.NameMaxLength);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(ModuleConstants.Profile.NameMaxLength);
        RuleFor(x => x.MiddleName).MaximumLength(ModuleConstants.Profile.NameMaxLength);
        RuleFor(x => x.Salutation).MaximumLength(ModuleConstants.Profile.SalutationMaxLength);

        // Only a new rep needs a login identifier supplied: on edit the account already has one, and an empty
        // email list leaves it untouched.
        RuleFor(x => x.Emails)
            .Must((salesRep, _) => HasLoginIdentifier(salesRep))
            .When(x => string.IsNullOrEmpty(x.Id))
            .WithMessage("A Sales Rep requires a login email (or user name).");

        RuleForEach(x => x.Addresses).ChildRules(address =>
        {
            address.RuleFor(x => x.CountryCode).NotEmpty();
            address.RuleFor(x => x.City).NotEmpty();
            address.RuleFor(x => x.Line1).NotEmpty();
            address.RuleFor(x => x.PostalCode).NotEmpty();
        });
    }

    private static bool HasLoginIdentifier(SalesRepDetails salesRep)
    {
        return !string.IsNullOrWhiteSpace(salesRep.UserName)
            || salesRep.Emails?.Any(x => !string.IsNullOrWhiteSpace(x)) == true;
    }
}
