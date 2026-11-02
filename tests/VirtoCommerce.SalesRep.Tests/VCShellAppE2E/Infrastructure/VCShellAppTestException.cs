using System;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>A failed VC-Shell app test, its message extended with what <see cref="VCShellSession"/> recorded.</summary>
internal sealed class VCShellAppTestException(string message, Exception inner) : Exception(message, inner);
