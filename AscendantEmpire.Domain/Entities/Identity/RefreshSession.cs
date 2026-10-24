using AscendantEmpire.Domain.Enums.Authentication;

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
/// One row covers a whole chain of rotations rather than a single token: <see cref="FamilyId"/> stays constant from login to logout, while
/// <see cref="TokenHash"/> moves to <see cref="PreviousTokenHash"/> and is replaced on each refresh. Keeping the retired hash is what makes a replayed
/// token detectable — see <see cref="PreviousTokenHash"/> for the one-generation limit that implies.
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
    /// Groups every rotation descended from one login, so a compromised chain can be revoked whole.
    /// </summary>
    /// <remarks>
    /// Assigned once when the session is issued and never changed afterwards. Because rotation reuses the same row, a family holds a single session
    /// today; the column exists so that detecting reuse revokes "this login" as a concept rather than "this row", and so a later design that forks a row
    /// per generation needs no schema change here.
    /// </remarks>
    public Guid FamilyId { get; set; }

    /// <summary>
    /// SHA-256 hash of the opaque refresh token. Replaced on every refresh, with the outgoing value moved to <see cref="PreviousTokenHash"/>. Unique
    /// across all sessions (enforced by an index in the DbContext) so a token maps to exactly one session.
    /// </summary>
    [MaxLength(TOKEN_HASH_MAX_LENGTH)]
    public required string TokenHash { get; set; }

    /// <summary>
    /// The hash retired by the most recent rotation, or null before the session has ever been refreshed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Presenting this token again is the signal that someone holds a copy they should not: the legitimate client already exchanged it and moved on.
    /// Within a short grace window it is instead read as a benign double-refresh — two browser tabs racing — and the session simply rotates again.
    /// </para>
    /// <para>
    /// Only one generation of history is kept, which is a deliberate trade-off rather than an oversight. A token two or more rotations old is already
    /// unusable and reads as unknown, so replaying it is not detected; the realistic attack — steal a token, then race the legitimate client — is always
    /// a replay of the most recent generation, and that is caught. Detecting a replay of any historical token would need a child table recording every
    /// hash ever issued.
    /// </para>
    /// </remarks>
    [MaxLength(TOKEN_HASH_MAX_LENGTH)]
    public string? PreviousTokenHash { get; set; }

    /// <summary>When the session was first created. Set once at issue, and never touched by a rotation.</summary>
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the token was last rotated, or null if it never has been.</summary>
    /// <remarks>
    /// Also the clock the reuse grace window is measured from, which is what lets a retired hash presented moments after its rotation be told apart from
    /// the same hash presented hours later.
    /// </remarks>
    public DateTimeOffset? LastRotatedAt { get; set; }

    /// <summary>Absolute expiry. After this the refresh token is rejected even if not revoked.</summary>
    public required DateTimeOffset ExpiresAt { get; set; }

    /// <summary>When the session was revoked, or null while it is still active.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Why the session was revoked, or null while it is still active.</summary>
    /// <remarks>Always written together with <see cref="RevokedAt"/>, so the two are either both set or both null.</remarks>
    public RefreshRevocationReason? RevokedReason { get; set; }

    /// <summary>The entered game, or null on a session that has not entered a game yet.</summary>
    public int? GameId { get; set; }

    /// <summary>The player controlled in the entered game, or null when no game is entered.</summary>
    public int? PlayerId { get; set; }

    /// <summary>
    /// Concurrency token guarding the rotation itself.
    /// </summary>
    /// <remarks>
    /// Two refreshes presenting the same token both pass the active check and both write a new hash. Without this the second write wins silently and the
    /// first client is left holding a token that will never work; with it the loser gets a <c>DbUpdateConcurrencyException</c> and can re-read and retry.
    /// </remarks>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>True when the session is neither revoked nor past its expiry at the given instant.</summary>
    public bool IsActive(DateTimeOffset now)
    {
        return RevokedAt is null && now < ExpiresAt;
    }
}
