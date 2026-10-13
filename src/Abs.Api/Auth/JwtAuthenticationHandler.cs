using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Abs.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Abs.Api.Auth;

/// <summary>
/// Validates JWTs issued by the Node.js backend.
/// Accepts: Authorization: Bearer header or ?token= query param.
/// JWT payload uses "userId" claim (not standard "sub").
/// </summary>
public class JwtAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AbsDbContext db,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtractToken();
        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        // Docker Compose passes JWT_SECRET_KEY as env var; ASP.NET Core maps it to config key "JWT_SECRET_KEY"
        var secret = configuration["JWT_SECRET_KEY"];
        if (string.IsNullOrEmpty(secret))
            return AuthenticateResult.Fail("JWT_SECRET_KEY is not configured.");

        ClaimsPrincipal principal;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                RequireExpirationTime = false,   // old Node tokens have no exp
                ClockSkew = TimeSpan.Zero,
                IssuerSigningKey = key
            };

            principal = handler.ValidateToken(token, validationParams, out _);
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail($"Token validation failed: {ex.Message}");
        }

        // Node uses "userId" claim, not standard ClaimTypes.NameIdentifier
        var userId = principal.FindFirstValue("userId");
        if (string.IsNullOrEmpty(userId))
            return AuthenticateResult.Fail("Token missing userId claim.");

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return AuthenticateResult.Fail("User not found.");

        if (user.IsActive != true)
            return AuthenticateResult.Fail("User is not active.");

        // Build identity with userId and role (type) claims
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Type ?? string.Empty)
        ], Scheme.Name);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private string? ExtractToken()
    {
        // 1. Authorization: Bearer <token>
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authHeader["Bearer ".Length..].Trim();

        // 2. ?token=<token>
        if (Request.Query.TryGetValue("token", out var queryToken))
            return queryToken.ToString();

        return null;
    }
}
