using AscendantEmpire.Application.Features.Authentication.Classes;

namespace AscendantEmpire.Api.Authentication;

/// <summary>
/// Builds the signed JWTs the client uses as bearer tokens.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// A token proving who the user is, with no game context yet. Issued by login/register.
    /// </summary>
    string CreateUserToken(AuthenticatedUser user);

    /// <summary>
    /// A token that also carries the entered game and the player controlled in it. Issued by
    /// enter-game. Replaces the user token on the client.
    /// </summary>
    string CreateGameToken(GameContext context);
}
