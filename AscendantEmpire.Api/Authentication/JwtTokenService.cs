using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Domain.Enums.Authentication;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AscendantEmpire.Api.Authentication;

/// <summary>
/// Creates signed JWTs using the symmetric signing key from <see cref="JwtSettings"/>.
/// </summary>
public class JwtTokenService(IOptions<JwtSettings> settings) : ITokenService
{
    private readonly JwtSettings _settings = settings.Value;

    public string CreateUserToken(AuthenticatedUser user, int sessionId)
    {
        // Base identity claims, present in every token.
        List<Claim> claims =
        [
            new Claim(AuthClaimTypes.UserId, user.UserId.ToString()),
            new Claim(AuthClaimTypes.Username, user.Username),
            new Claim(AuthClaimTypes.SessionId, sessionId.ToString()),
        ];

        AddPermissionClaims(claims, user.Permissions);

        return BuildToken(claims);
    }

    public string CreateGameToken(GameContext context, int sessionId)
    {
        // User identity plus the entered game and the controlled player.
        List<Claim> claims =
        [
            new Claim(AuthClaimTypes.UserId, context.UserId.ToString()),
            new Claim(AuthClaimTypes.Username, context.Username),
            new Claim(AuthClaimTypes.SessionId, sessionId.ToString()),
            new Claim(AuthClaimTypes.GameId, context.GameId.ToString()),
            new Claim(AuthClaimTypes.PlayerId, context.PlayerId.ToString()),
        ];

        AddPermissionClaims(claims, context.Permissions);

        return BuildToken(claims);
    }

    /// <summary>
    /// Appends one claim per granted permission, named after the enum member.
    /// </summary>
    /// <remarks>
    /// Repeated single-valued claims rather than one delimited value: that is what the policies built with <c>RequireClaim</c> match, and it keeps a
    /// permission name from ever having to be escaped.
    /// </remarks>
    private static void AddPermissionClaims(List<Claim> claims, IReadOnlyCollection<PermissionType> permissions)
    {
        foreach (PermissionType permission in permissions)
        {
            claims.Add(new Claim(AuthClaimTypes.Permission, permission.ToString()));
        }
    }

    /// <summary>
    /// Signs the given claims into a JWT string with the configured issuer, audience and lifetime.
    /// </summary>
    private string BuildToken(IEnumerable<Claim> claims)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_settings.SigningKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
