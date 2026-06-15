namespace AscendantEmpire.Api.Authentication;

/// <summary>
/// Strongly-typed JWT configuration, bound from the "Jwt" section of app settings.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>Who issues the token (this API). Validated on incoming tokens.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Who the token is for (this API). Validated on incoming tokens.</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// The secret key used to sign and verify tokens. Keep this out of source control —
    /// set it via user-secrets or an environment variable. Must be long (32+ chars).
    /// </summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>How long an issued token stays valid, in minutes.</summary>
    public int AccessTokenMinutes { get; init; } = 120;
}
