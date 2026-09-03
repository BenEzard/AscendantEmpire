using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Games;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Games;

public class Player
{
    public const int RULER_TITLE_MAX_LENGTH = 50;
    public const int RULER_NAME_MAX_LENGTH = 50;
    public const int FACTION_NAME_MAX_LENGTH = 50;

    /// <summary>
    /// Long enough for "#rrggbbaa", although a faction colour is always "#rrggbb" today.
    /// </summary>
    public const int FACTION_COLOUR_MAX_LENGTH = 9;

    /// <summary>
    /// Stands in for a ruler title the player has not chosen yet.
    /// </summary>
    /// <remarks>
    /// Applied when a game seeds its empty slots, and again when a player saves their setup without picking a title. It is a placeholder rather
    /// than a database default so that the substitution happens in one place we control.
    /// </remarks>
    public const string DEFAULT_RULER_TITLE = "Nobody";

    [Key]
    public int Id { get; set; }
    public int GameId { get; set; }

    /// <summary>
    /// A User can be null when the Player slot is unassigned.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// The account that controls this Player. Null when the slot is unassigned.
    /// </summary>
    public User? User { get; set; }

    [MaxLength(RULER_TITLE_MAX_LENGTH)]
    public required string RulerTitle { get; set; }

    [MaxLength(RULER_NAME_MAX_LENGTH)]
    public required string RulerName { get; set; }

    /// <summary>
    /// The gender of this player's ruler, or null when the player has not defined themselves yet.
    /// </summary>
    /// <remarks>
    /// Nullable because a seeded slot deliberately asserts no gender - it carries no more than a placeholder title and name until someone claims
    /// it. That also makes "has this player completed setup?" answerable without a second flag.
    /// </remarks>
    public RulerGender? RulerGender { get; set; }

    [MaxLength(FACTION_NAME_MAX_LENGTH)]
    public required string FactionName { get; set; }

    /// <summary>
    /// The HTML colour this faction is drawn in, or null while the player has not picked one.
    /// </summary>
    /// <remarks>
    /// Stored lower-cased so that a clash is detectable without relying on the database collation. Unique within a game - see the
    /// UX_Player_Game_FactionColour index.
    /// </remarks>
    [MaxLength(FACTION_COLOUR_MAX_LENGTH)]
    public string? FactionColour { get; set; }

    public PlayerStatus Status { get; set; }
    public int CampaignOffsetId { get; set; }

    public GameIndex Game { get; set; } = null!;
}
