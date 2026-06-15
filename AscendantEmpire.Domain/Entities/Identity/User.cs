using AscendantEmpire.Domain.Entities.Games;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Identity;

/// <summary>
/// An account that a person logs in with. A User owns one or more <see cref="Player"/>
/// records — at most one Player per Game, but possibly many Games at once.
/// </summary>
public class User
{
    public const int USERNAME_MAX_LENGTH = 50;
    public const int EMAIL_MAX_LENGTH = 256;

    /// <summary>
    /// Hashed passwords produced by <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/>
    /// are well under this length; the generous cap leaves headroom for future hasher changes.
    /// </summary>
    public const int PASSWORD_HASH_MAX_LENGTH = 256;

    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The login name. Unique across all users (enforced by a unique index in the DbContext).
    /// </summary>
    [MaxLength(USERNAME_MAX_LENGTH)]
    public required string Username { get; set; }

    /// <summary>
    /// Optional contact email. Not used for login in this first pass.
    /// </summary>
    [MaxLength(EMAIL_MAX_LENGTH)]
    public string? Email { get; set; }

    /// <summary>
    /// The hashed (never plaintext) password. Produced and verified by the password hasher.
    /// </summary>
    [MaxLength(PASSWORD_HASH_MAX_LENGTH)]
    public required string PasswordHash { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The Players this user controls across all their games.
    /// </summary>
    public ICollection<Player> Players { get; set; } = [];
}
