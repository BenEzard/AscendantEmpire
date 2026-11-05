namespace AscendantEmpire.Api.Authentication;

/// <summary>
/// Strongly-typed JWT configuration, bound from the "Jwt" section of app settings.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// The shortest signing key HS256 will accept, in UTF-8 bytes.
    /// </summary>
    /// <remarks>
    /// Fixed by the algorithm rather than chosen: HMAC-SHA256 refuses a key below its own 256-bit output size, so a shorter one throws out of
    /// <c>CreateKeyedHashAlgorithm</c> when the first token is signed. Startup checks against this so a short key is caught on boot instead.
    /// </remarks>
    public const int MINIMUM_SIGNING_KEY_BYTES = 32;

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
    /// Keep this out of source control — set it via user-secrets or an environment variable. At least
    /// <see cref="MINIMUM_SIGNING_KEY_BYTES"/> bytes, which startup enforces.
    /// </remarks>
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
