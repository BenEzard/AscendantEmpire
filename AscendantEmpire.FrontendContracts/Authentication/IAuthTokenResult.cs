namespace AscendantEmpire.FrontendContracts.Authentication;

/// <summary>
/// The result of an authentication call. Carries the bearer token the client should send on
/// subsequent requests, plus the identity baked into it for convenience. After login/register
/// only <see cref="Username"/> is set; after entering a game <see cref="GameId"/> and
/// <see cref="PlayerId"/> are populated too.
/// </summary>
public interface IAuthTokenResult
{
    string Token { get; }
    string Username { get; }

    /// <summary>
    /// The refresh token used to obtain a new access token when the short-lived <see cref="Token"/>
    /// expires. Set by login/register/refresh; null on enter-game (the session, and therefore its
    /// refresh token, is unchanged — the client keeps the one it already holds).
    /// </summary>
    string? RefreshToken { get; }

    int? GameId { get; }
    int? PlayerId { get; }
}
