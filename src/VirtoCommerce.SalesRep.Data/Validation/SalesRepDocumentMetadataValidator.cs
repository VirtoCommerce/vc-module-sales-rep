using System;
using System.Buffers;
using System.Linq;
using FluentValidation;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Data.Models;

namespace VirtoCommerce.SalesRep.Data.Validation;

public class SalesRepDocumentMetadataValidator : AbstractValidator<SalesRepDocumentMetadata>
{
    // Fixed rejected set (not the OS-dependent Path.GetInvalidFileNameChars) so a category is accepted or rejected identically on Windows and Linux.
    private static readonly SearchValues<char> _invalidCategoryChars = SearchValues.Create("<>:\"/\\|?*");

    public SalesRepDocumentMetadataValidator()
    {
        RuleFor(x => x.FileId).NotEmpty().MaximumLength(DocumentMetadataEntity.FileIdLength);

        RuleFor(x => x.Category)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(ModuleConstants.Documents.CategoryMaxLength)
            .Must(BeAValidCategoryName).WithMessage(x => $"Invalid category name '{x.Category}'.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(DocumentMetadataEntity.NameLength);
        RuleFor(x => x.Summary).MaximumLength(DocumentMetadataEntity.SummaryLength);
        RuleFor(x => x.PreviewUrl).MaximumLength(DocumentMetadataEntity.PreviewUrlLength);
        RuleFor(x => x.PageCount).GreaterThan(0);
    }

    private static bool BeAValidCategoryName(string category)
    {
        return !category.Contains("..") &&
            category.AsSpan().IndexOfAny(_invalidCategoryChars) < 0 &&
            !category.Any(char.IsControl);
    }
}
