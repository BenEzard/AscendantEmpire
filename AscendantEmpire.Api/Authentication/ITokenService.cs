using AscendantEmpire.Application.Features.Authentication.Classes;

namespace AscendantEmpire.Api.Authentication;

/// <summary>
/// Builds the signed JWTs the client uses as bearer tokens.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// A token proving who the user is, with no game context yet. Issued by login/register.
    /// <paramref name="sessionId"> is the refresh session it was issued from.</paramref>
    /// </summary>
    string CreateUserToken(AuthenticatedUser user, int sessionId);

    /// <summary>
    /// A token that also carries the entered game and the player controlled in it.
    /// </summary>
    /// <remarks>
    /// Issued by enter-game. Replaces the user token on the client. <paramref name="sessionId"/> is the refresh session it was issued from.
    /// </remarks>
    string CreateGameToken(GameContext context, int sessionId);
}
