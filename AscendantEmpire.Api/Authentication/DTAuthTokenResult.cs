using AscendantEmpire.Domain.Enums.Authentication;
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

    /// <summary>
    /// The capabilities the accompanying token grants.
    /// </summary>
    /// <remarks>
    /// Returned alongside the token so the client can hide tools the user cannot reach without decoding the JWT itself. This is a convenience for the UI
    /// only — the server never trusts it, and every gate is enforced by the token's claims.
    /// </remarks>
    public required IReadOnlyCollection<PermissionType> Permissions { get; init; }
}
