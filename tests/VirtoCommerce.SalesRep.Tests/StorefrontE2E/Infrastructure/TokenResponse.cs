using System;
using System.Text.Json;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

internal sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn)
{
    /// <summary>The storefront's localStorage.auth shape (what useAuth.ts stores after /connect/token).</summary>
    public string ToLocalStorageJson() => JsonSerializer.Serialize(new
    {
        expires_at = DateTime.UtcNow.AddSeconds(ExpiresIn).ToString("O"),
        token_type = "Bearer",
        access_token = AccessToken,
        refresh_token = RefreshToken,
    });
}
