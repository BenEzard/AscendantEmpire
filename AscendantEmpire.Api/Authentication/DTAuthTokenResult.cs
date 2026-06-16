using AscendantEmpire.FrontendContracts.Authentication;

namespace AscendantEmpire.Api.Authentication;

/// <summary>
/// Concrete <see cref="IAuthTokenResult"/> returned by the auth endpoints.
/// </summary>
public class DTAuthTokenResult : IAuthTokenResult
{
    public required string Token { get; init; }
    public required string Username { get; init; }
    public string? RefreshToken { get; init; }
    public int? GameId { get; init; }
    public int? PlayerId { get; init; }
}
