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
    int? GameId { get; }
    int? PlayerId { get; }
}
