using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.OpenIddict;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// Mints the bearer token the storefront stores in localStorage.auth. The claims are produced by the REAL platform
/// and customer-module code (<c>IUserClaimsPrincipalFactory</c> + every <c>ITokenClaimProvider</c>), so the token
/// carries what a production /connect/token issues: sub, name, email, role, permission(s), memberId,
/// organization_id. Only the signature is test-owned (a symmetric key the host validates).
/// </summary>
internal sealed class TestTokenService(IServiceProvider services)
{
    public const string Issuer = "http://salesrep-storefront-e2e/";
    public const string Audience = "resource_server";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    public static readonly SymmetricSecurityKey SigningKey =
        new(SHA256.HashData(Encoding.UTF8.GetBytes("VirtoCommerce.SalesRep storefront E2E signing key")));

    private readonly ConcurrentDictionary<string, string> _refreshTokens = new();

    /// <summary>A token for an existing account, as a test signs a browser in without the sign-in page.</summary>
    public async Task<TokenResponse> IssueAsync(string userId, string organizationId = null)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException($"No account with id '{userId}'.");

        return await IssueForAsync(scope.ServiceProvider, user, organizationId);
    }

    /// <summary>The password grant, for tests that go through the storefront's own sign-in page.</summary>
    public async Task<TokenResponse> IssueByPasswordAsync(string userName, string password, string organizationId = null)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);

        if (user == null || !await userManager.CheckPasswordAsync(user, password))
        {
            return null;
        }

        return await IssueForAsync(scope.ServiceProvider, user, organizationId);
    }

    public Task<TokenResponse> RefreshAsync(string refreshToken)
        => _refreshTokens.TryRemove(refreshToken ?? string.Empty, out var userId) ? IssueAsync(userId) : Task.FromResult<TokenResponse>(null);

    private async Task<TokenResponse> IssueForAsync(IServiceProvider scoped, ApplicationUser user, string organizationId)
    {
        // 1. The platform's principal: sub, name, email, security stamp, role(s) and the roles' permission claims.
        var principal = await scoped.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);

        // 2. The modules' token claim providers (customer module: organization_id + organization-scoped permissions).
        var request = new OpenIddictRequest();
        if (!string.IsNullOrEmpty(organizationId))
        {
            request.SetParameter("organization_id", organizationId);
        }

        var context = new TokenRequestContext { User = user, Principal = principal, Request = request };
        foreach (var provider in scoped.GetServices<ITokenClaimProvider>())
        {
            await provider.SetClaimsAsync(principal, context);
        }

        var securityStampClaimType = scoped.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity.SecurityStampClaimType;
        var claims = principal.Claims
            .Where(x => x.Type != securityStampClaimType)
            .Select(x => new Claim(x.Type, x.Value))
            .ToList();

        var now = DateTime.UtcNow;
        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(Lifetime),
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
        });

        var refreshToken = Guid.NewGuid().ToString("N");
        _refreshTokens[refreshToken] = user.Id;

        return new TokenResponse(accessToken, refreshToken, (int)Lifetime.TotalSeconds);
    }
}

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
