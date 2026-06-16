namespace AscendantEmpire.Api.Authentication;

/// <summary>
/// Strongly-typed JWT configuration, bound from the "Jwt" section of app settings.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Who issues the token (this API). 
    /// </summary>
    /// <remarks>Validated on incoming tokens.</remarks>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Who the token is for (this API). Validated on incoming tokens.</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// The secret key used to sign and verify tokens. 
    /// </summary>
    /// <remarks>
    /// Keep this out of source control — set it via user-secrets or an environment variable. Must be long (32+ chars).
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>
    /// How long an issued access token stays valid, in minutes. 
    /// </summary>
    /// <remarks>
    /// Kept short so that revoking a session takes effect within this window (the client can no longer refresh once revoked).
    /// </remarks>
    public int AccessTokenMinutes { get; init; } = 5;

    /// <summary>
    /// How long a refresh token (and its backing session) stays valid, in days. 
    /// </summary>
    /// <remarks>This is the maximum a user can stay logged in without re-entering credentials.</remarks>
    public int RefreshTokenDays { get; init; } = 14;
}
