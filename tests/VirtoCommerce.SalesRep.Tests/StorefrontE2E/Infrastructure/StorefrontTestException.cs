using System;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>A failed storefront test, its message extended with what <see cref="StorefrontSession"/> recorded.</summary>
internal sealed class StorefrontTestException(string message, Exception inner) : Exception(message, inner);
