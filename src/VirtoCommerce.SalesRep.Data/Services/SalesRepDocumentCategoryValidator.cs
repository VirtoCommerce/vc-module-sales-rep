using System;
using System.IO;
using VirtoCommerce.SalesRep.Core;

namespace VirtoCommerce.SalesRep.Data.Services;

internal static class SalesRepDocumentCategoryValidator
{
    // The category is a plain metadata value now (blobs are stored flat), but the old path-segment
    // hygiene rules are kept as a value check so categories stay safe to echo into URLs and UIs.
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
            value.Contains('/') ||
            value.Contains('\\') ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"Invalid category name '{category}'.", nameof(category));
        }

        return value;
    }
}
