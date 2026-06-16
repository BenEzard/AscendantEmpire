namespace AscendantEmpire.Application.Features.Authentication.Classes;

/// <summary>
/// The result of refreshing a session. 
/// </summary>
/// <remarks>
/// Carries everything the API needs to mint a new access token (the identity plus any stored game context) along with the freshly rotated raw refresh token to return to the client. 
/// When <see cref="GameId"/>/<see cref="PlayerId"/> are set the API should issue a game-scoped token; otherwise a plain user token.
/// </remarks>
public record RefreshedSession(
    int SessionId,
    int UserId,
    string Username,
    int? GameId,
    int? PlayerId,
    string RefreshToken
);
