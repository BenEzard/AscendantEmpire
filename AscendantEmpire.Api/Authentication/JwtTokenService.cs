using AscendantEmpire.Application.Features.Authentication.Classes;

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

    public string CreateUserToken(AuthenticatedUser user)
    {
        // Base identity claims, present in every token.
        List<Claim> claims =
        [
            new Claim(AuthClaimTypes.UserId, user.UserId.ToString()),
            new Claim(AuthClaimTypes.Username, user.Username),
        ];

        return BuildToken(claims);
    }

    public string CreateGameToken(GameContext context)
    {
        // User identity plus the entered game and the controlled player.
        List<Claim> claims =
        [
            new Claim(AuthClaimTypes.UserId, context.UserId.ToString()),
            new Claim(AuthClaimTypes.Username, context.Username),
            new Claim(AuthClaimTypes.GameId, context.GameId.ToString()),
            new Claim(AuthClaimTypes.PlayerId, context.PlayerId.ToString()),
        ];

        return BuildToken(claims);
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
