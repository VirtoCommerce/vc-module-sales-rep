using System.Linq;
using FluentAssertions;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Data.Validation;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The document metadata validation rules enforced on every save (the validator runs in
/// SalesRepDocumentMetadataService.BeforeSaveChanges): the file link and category are required, the category
/// respects the business length cap and rejects path/control characters identically on Windows and Linux,
/// and the optional text fields respect their column lengths.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepDocumentMetadataValidatorTests
{
    private readonly SalesRepDocumentMetadataValidator _validator = new();

    [Fact]
    public void Validate_ValidMetadata_Passes()
    {
        _validator.Validate(CreateMetadata()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingFileId_Fails(string fileId)
    {
        var result = _validator.Validate(CreateMetadata(x => x.FileId = fileId));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(SalesRepDocumentMetadata.FileId));
    }

    // The rejected characters are an explicit OS-independent set — every case must fail on Windows and Linux alike.
    // Whitespace-only values are pre-trimmed to empty by BeforeSaveChanges; here they hit the char rules directly.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../etc")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("bad|name")]
    [InlineData("bad:name")]
    [InlineData("bad\tname")]
    [InlineData("this-category-is-over-32-chars-long")] // > ModuleConstants.Documents.CategoryMaxLength
    public void Validate_InvalidCategory_Fails(string category)
    {
        var result = _validator.Validate(CreateMetadata(x => x.Category = category));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(SalesRepDocumentMetadata.Category));
    }

    [Fact]
    public void Validate_OverlongOptionalFields_Fail()
    {
        var result = _validator.Validate(CreateMetadata(x =>
        {
            x.Name = new string('n', 513);
            x.Summary = new string('s', 2049);
            x.PreviewUrl = new string('u', 2084);
        }));

        result.Errors.Select(x => x.PropertyName).Should().BeEquivalentTo(
            nameof(SalesRepDocumentMetadata.Name),
            nameof(SalesRepDocumentMetadata.Summary),
            nameof(SalesRepDocumentMetadata.PreviewUrl));
    }

    private static SalesRepDocumentMetadata CreateMetadata(System.Action<SalesRepDocumentMetadata> mutate = null)
    {
        var metadata = new SalesRepDocumentMetadata
        {
            FileId = "file-1",
            Category = "Catalogs",
        };
        mutate?.Invoke(metadata);
        return metadata;
    }
}
