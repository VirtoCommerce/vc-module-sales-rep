using System;
using System.Linq;
using VirtoCommerce.SalesRep.Core;

namespace VirtoCommerce.SalesRep.Data.Services;

internal static class SalesRepDocumentCategoryValidator
{
    // Fixed rejected set (not the OS-dependent Path.GetInvalidFileNameChars) so a category is accepted or rejected identically on Windows and Linux.
    private static readonly char[] InvalidCategoryChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public static string Sanitize(string category, bool required)
    {
        var value = category?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return required ? throw new ArgumentException("Category is required.", nameof(category)) : null;
        }

        if (value.Length > ModuleConstants.Documents.CategoryMaxLength)
        {
            throw new ArgumentException($"Category must be {ModuleConstants.Documents.CategoryMaxLength} characters or less.", nameof(category));
        }

        if (value.Contains("..") ||
            value.IndexOfAny(InvalidCategoryChars) >= 0 ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException($"Invalid category name '{category}'.", nameof(category));
        }

        return value;
    }
}
