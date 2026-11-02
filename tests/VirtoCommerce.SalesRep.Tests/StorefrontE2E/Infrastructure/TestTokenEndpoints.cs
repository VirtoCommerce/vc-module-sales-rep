using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The two auth endpoints the storefront calls: the password and refresh grants of /connect/token (form-encoded,
/// exactly what useAuth.ts posts) and /revoke/token. Enough for the sign-in page; not an OAuth server.
/// </summary>
internal static class TestTokenEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/connect/token", async (HttpRequest request, TestTokenService tokens) =>
        {
            var form = await request.ReadFormAsync();
            var organizationId = form["organization_id"].ToString().EmptyToNull();

            var token = form["grant_type"].ToString() switch
            {
                "password" => await tokens.IssueByPasswordAsync(form["username"].ToString(), form["password"].ToString(), organizationId),
                "refresh_token" => await tokens.RefreshAsync(form["refresh_token"].ToString()),
                _ => null,
            };

            return token == null
                ? Results.BadRequest(new { error = "invalid_grant" })
                : Results.Json(new { access_token = token.AccessToken, refresh_token = token.RefreshToken, token_type = "Bearer", expires_in = token.ExpiresIn });
        });

        app.MapPost("/revoke/token", () => Results.Ok());
    }
}
