using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Identity;

/// <summary>
/// A server-side login session backing a single refresh token. Issued at login/register and
/// rotated on every refresh. Because the session lives in the database it can be revoked
/// (e.g. when a game is deleted or a player is kicked), which the stateless access token alone
/// cannot be.
/// </summary>
/// <remarks>
/// <para>
/// The raw refresh token is never stored — only its SHA-256 hash in <see cref="TokenHash"/>.
/// The client presents the raw token; the server hashes it and looks the session up by hash.
/// </para>
/// <para>
/// The game context (<see cref="GameId"/>/<see cref="PlayerId"/>) is held on the session so that
/// refreshing can re-issue a correctly-scoped access token, and so revocation can target a single
/// game's sessions.
/// </para>
/// </remarks>
public class RefreshSession
{
    /// <summary>
    /// A SHA-256 hex digest is 64 characters; the cap leaves headroom for a future hash change.
    /// </summary>
    public const int TOKEN_HASH_MAX_LENGTH = 128;

    [Key]
    public int Id { get; set; }

    /// <summary>The account this session belongs to.</summary>
    public int UserId { get; set; }

    /// <summary>Navigation to the owning user.</summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// SHA-256 hash of the opaque refresh token. Rotated in place on every refresh. Unique across
    /// all sessions (enforced by an index in the DbContext) so a token maps to exactly one session.
    /// </summary>
    [MaxLength(TOKEN_HASH_MAX_LENGTH)]
    public required string TokenHash { get; set; }

    /// <summary>When the session was first created (refreshed in place on rotation).</summary>
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>Absolute expiry. After this the refresh token is rejected even if not revoked.</summary>
    public required DateTimeOffset ExpiresAt { get; set; }

    /// <summary>When the session was revoked, or null while it is still active.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>The entered game, or null on a session that has not entered a game yet.</summary>
    public int? GameId { get; set; }

    /// <summary>The player controlled in the entered game, or null when no game is entered.</summary>
    public int? PlayerId { get; set; }

    /// <summary>True when the session is neither revoked nor past its expiry at the given instant.</summary>
    public bool IsActive(DateTimeOffset now)
    {
        return RevokedAt is null && now < ExpiresAt;
    }
}
