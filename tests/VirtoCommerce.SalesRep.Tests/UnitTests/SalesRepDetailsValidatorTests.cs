using System;
using FluentAssertions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Data.Validation;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The Sales Rep profile rules enforced on every save (the validator runs in SalesRepService.SaveOneAsync, before
/// anything is persisted): first and last name are required because the storefront X-API publishes
/// contact.firstName/.lastName/.fullName as non-null GraphQL fields - a nameless rep cannot resolve the sign-in
/// page context and is locked out of the storefront (VCST-5759). A new rep also needs a login identifier, and any
/// address supplied must carry the parts the storefront and shipping need.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepDetailsValidatorTests
{
    private readonly SalesRepDetailsValidator _validator = new();

    [Fact]
    public void Validate_ValidRep_Passes()
    {
        _validator.Validate(CreateRep()).IsValid.Should().BeTrue();
    }

    // Whitespace-only values are pre-trimmed to empty by SalesRepService.Normalize; NotEmpty rejects them either way.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingFirstName_Fails(string firstName)
    {
        var result = _validator.Validate(CreateRep(x => x.FirstName = firstName));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(SalesRepDetails.FirstName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingLastName_Fails(string lastName)
    {
        var result = _validator.Validate(CreateRep(x => x.LastName = lastName));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(SalesRepDetails.LastName));
    }

    // The name parts are stored in 128-char ContactEntity columns; over-long input must be a 400, not a DB error.
    [Fact]
    public void Validate_OverlongNames_Fail()
    {
        var tooLong = new string('a', 129);

        var result = _validator.Validate(CreateRep(x =>
        {
            x.FirstName = tooLong;
            x.MiddleName = tooLong;
            x.LastName = tooLong;
        }));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(SalesRepDetails.FirstName));
        result.Errors.Should().Contain(x => x.PropertyName == nameof(SalesRepDetails.MiddleName));
        result.Errors.Should().Contain(x => x.PropertyName == nameof(SalesRepDetails.LastName));
    }

    // Middle name stays optional - only the two fields the non-null GraphQL contract depends on are required.
    [Fact]
    public void Validate_MissingMiddleName_Passes()
    {
        _validator.Validate(CreateRep(x => x.MiddleName = null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NewRepWithoutLoginIdentifier_Fails()
    {
        var result = _validator.Validate(CreateRep(x => x.Emails = []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorMessage.Contains("login email", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NewRepWithUserNameOnly_Passes()
    {
        _validator.Validate(CreateRep(x =>
        {
            x.Emails = [];
            x.UserName = "jane.rep";
        })).IsValid.Should().BeTrue();
    }

    // On edit the account already carries a login, and an empty email list leaves it untouched - so the
    // login-identifier rule is create-only, keyed off the id the way SalesRepService keys "is new".
    [Fact]
    public void Validate_ExistingRepWithoutLoginIdentifier_Passes()
    {
        _validator.Validate(CreateRep(x =>
        {
            x.Id = "rep-1";
            x.Emails = [];
        })).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(Address.CountryCode))]
    [InlineData(nameof(Address.City))]
    [InlineData(nameof(Address.Line1))]
    [InlineData(nameof(Address.PostalCode))]
    public void Validate_AddressMissingRequiredPart_Fails(string property)
    {
        var result = _validator.Validate(CreateRep(x => x.Addresses = [ClearAddressPart(property)]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == $"Addresses[0].{property}");
    }

    [Fact]
    public void Validate_CompleteAddress_Passes()
    {
        _validator.Validate(CreateRep(x => x.Addresses = [CreateAddress()])).IsValid.Should().BeTrue();
    }

    private static SalesRepDetails CreateRep(Action<SalesRepDetails> setup = null)
    {
        var rep = new SalesRepDetails
        {
            FirstName = "Jane",
            MiddleName = "Q",
            LastName = "Rep",
            Emails = ["jane@test.com"],
        };

        setup?.Invoke(rep);

        return rep;
    }

    private static Address CreateAddress()
    {
        return new Address
        {
            CountryCode = "USA",
            CountryName = "United States",
            City = "Los Angeles",
            Line1 = "1 Main St",
            PostalCode = "90001",
        };
    }

    private static Address ClearAddressPart(string property)
    {
        var address = CreateAddress();
        typeof(Address).GetProperty(property)!.SetValue(address, null);
        return address;
    }
}
