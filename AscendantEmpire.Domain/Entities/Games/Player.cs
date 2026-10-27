using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Games;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Games;

public class Player
{
    public const int RULER_TITLE_MAX_LENGTH = 50;
    public const int RULER_NAME_MAX_LENGTH = 50;
    public const int FACTION_NAME_MAX_LENGTH = 50;
    public const int PEOPLE_NAME_MAX_LENGTH = 50;
    public const int DEMONYM_MAX_LENGTH = 50;
    public const int CULTURE_NAME_MAX_LENGTH = 50;
    public const int LANGUAGE_MAX_LENGTH = 50;

    /// <summary>
    /// The collation pinned on the identity columns that must be unique within a game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Case-insensitive and accent-sensitive, so "The Vashti" and "the vashti" are one name while "Vashti" and "Vashti'" are two. Pinned on the
    /// columns rather than inherited from the server, because the uniqueness rule those columns carry rests entirely on the comparison being
    /// case-insensitive - a database created on a <c>CS_AS</c> server would silently let two factions take the same name in different capitals.
    /// </para>
    /// <para>
    /// Spacing is not something a collation settles, which is why <c>UpdatePlayerDefinitionHandler</c> also collapses runs of internal whitespace
    /// before it stores one of these names.
    /// </para>
    /// </remarks>
    public const string IDENTITY_COLLATION = "Latin1_General_CI_AS";

    /// <summary>
    /// Long enough for "#rrggbbaa", although a faction colour is always "#rrggbb" today.
    /// </summary>
    public const int FACTION_COLOUR_MAX_LENGTH = 9;

    /// <summary>
    /// Generous for "/portraits/01_western_king.png", which is all this ever holds.
    /// </summary>
    /// <remarks>
    /// Deliberately shorter than the 500 allowed for an arbitrary catalogue icon path: this column holds one of a fixed set of shipped portrait
    /// paths, not something a user types.
    /// </remarks>
    public const int RULER_PORTRAIT_PATH_MAX_LENGTH = 100;

    /// <summary>
    /// Stands in for a ruler portrait the player has not chosen yet.
    /// </summary>
    /// <remarks>
    /// The one value <see cref="RulerPortraitPath"/> may hold more than once within a game - the UX_Player_Game_RulerPortraitPath index filters it out
    /// - because any number of players can be undecided at the same time, while a portrait somebody has actually chosen is theirs alone. Empty rather
    /// than a path to a placeholder image, so there is no file to ship and
    /// nothing for a client to special-case - every consumer already has to draw something for a portrait it cannot load.
    /// </remarks>
    public const string DEFAULT_RULER_PORTRAIT_PATH = "";

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

    /// <summary>
    /// The ruler's title, e.g. "Empress", or <see cref="DEFAULT_RULER_TITLE"/> while none has been chosen.
    /// </summary>
    /// <remarks>
    /// Deliberately not unique within a game - several rulers may all be kings - and the one part of a player's definition that stays editable for
    /// the whole game. A title is expected to change as an empire grows, where the names around it are what everything else labels and attributes by.
    /// </remarks>
    [MaxLength(RULER_TITLE_MAX_LENGTH)]
    public required string RulerTitle { get; set; }

    /// <summary>
    /// The ruler's own name, e.g. "Aelara".
    /// </summary>
    /// <remarks>
    /// <b>Unique within a game</b>, compared case-insensitively - see the UX_Player_Game_RulerName index and <see cref="IDENTITY_COLLATION"/>.
    /// </remarks>
    [MaxLength(RULER_NAME_MAX_LENGTH)]
    public required string RulerName { get; set; }

    /// <summary>
    /// The gender of this player's ruler, or null when the player has not defined themselves yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nullable because a seeded slot deliberately asserts no gender - it carries no more than a placeholder title and name until someone claims
    /// it. That also makes "has this player completed setup?" answerable without a second flag.
    /// </para>
    /// <para>
    /// That question is load-bearing rather than merely convenient: a game starts when every active slot is claimed <i>and</i> every one of those
    /// players has defined themselves, and a non-null gender here is the signal <c>UpdatePlayerDefinitionHandler</c> reads to decide it.
    /// </para>
    /// </remarks>
    public RulerGender? RulerGender { get; set; }

    /// <summary>
    /// What this faction is called, e.g. "The Ember Pact".
    /// </summary>
    /// <remarks>
    /// <b>Unique within a game</b>, compared case-insensitively - see the UX_Player_Game_FactionName index and <see cref="IDENTITY_COLLATION"/>.
    /// </remarks>
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

    /// <summary>
    /// Root-relative path to the portrait chosen for this ruler, e.g. "/portraits/07_northern_queen.png".
    /// </summary>
    /// <remarks>
    /// <para>
    /// A client path, not a server one, exactly like Asset.IconPath: the files ship in the UI's public folder and the API neither reads nor serves
    /// them. The value is one of the paths listed in the UI's RulerPortrait.ts, or <see cref="DEFAULT_RULER_PORTRAIT_PATH"/> for a player who has not
    /// chosen yet.
    /// </para>
    /// <para>
    /// Non-nullable and seeded with a placeholder like <see cref="RulerName"/>, rather than nullable like <see cref="FactionColour"/>, because the
    /// game toolbar shows the portrait of whichever player's turn is open: a nullable column would make every read of it decide what a missing
    /// portrait looks like.
    /// </para>
    /// <para>
    /// <b>Unique within a game once chosen.</b> Two players may not wear the same face, so a non-default value is rejected if another slot in the
    /// same game already holds it. Enforced by the UX_Player_Game_RulerPortraitPath filtered unique index, with the check in
    /// UpdatePlayerDefinitionHandler there to turn the race into a friendly error. The filter spares
    /// <see cref="DEFAULT_RULER_PORTRAIT_PATH"/>, so any number of players may be undecided at once.
    /// </para>
    /// </remarks>
    [MaxLength(RULER_PORTRAIT_PATH_MAX_LENGTH)]
    public required string RulerPortraitPath { get; set; }

    /// <summary>
    /// What this faction's people call themselves as a group, e.g. "The Vashti".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Text the player authors rather than a key - nothing is looked up by it - but <b>unique within a game</b> all the same, compared
    /// case-insensitively. See the UX_Player_Game_PeopleName index and <see cref="IDENTITY_COLLATION"/>. Anything that labels or attributes by this
    /// name needs it to pick out one faction unambiguously for the whole game, which is also why it stops being editable once play begins.
    /// </para>
    /// <para>
    /// Like <see cref="RulerName"/> and <see cref="FactionName"/> it is non-nullable and seeded with a placeholder, so every slot always has something
    /// to show in a listing even before anyone claims it.
    /// </para>
    /// </remarks>
    [MaxLength(PEOPLE_NAME_MAX_LENGTH)]
    public required string PeopleName { get; set; }

    /// <summary>
    /// What a single member of this people is called, e.g. "Vashti" for one of "The Vashti".
    /// </summary>
    /// <remarks>
    /// Held separately rather than derived from <see cref="PeopleName"/> because no rule turns one into the other reliably - the singular of a
    /// player-invented name is the player's to decide. <b>Unique within a game</b>, compared case-insensitively - see the UX_Player_Game_Demonym
    /// index and <see cref="IDENTITY_COLLATION"/>.
    /// </remarks>
    [MaxLength(DEMONYM_MAX_LENGTH)]
    public required string Demonym { get; set; }

    /// <summary>
    /// The name of this people's culture, e.g. "Vashti Highland".
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="PeopleName"/>: a people and the culture they carry are named separately so that the two can diverge as a game runs on.
    /// <b>Unique within a game</b>, compared case-insensitively - see the UX_Player_Game_CultureName index and <see cref="IDENTITY_COLLATION"/>.
    /// </remarks>
    [MaxLength(CULTURE_NAME_MAX_LENGTH)]
    public required string CultureName { get; set; }

    /// <summary>
    /// The language this people speak, e.g. "Old Vashti".
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain name, not a link to anything: there is no language catalogue, and the Language technology in the tech tree is unrelated to this field.
    /// </para>
    /// <para>
    /// The one name in this group that is <b>deliberately not unique</b> within a game. Two peoples sharing a language is a legitimate thing for a
    /// player to express, and nothing attributes anything by language, so there is no index over it.
    /// </para>
    /// </remarks>
    [MaxLength(LANGUAGE_MAX_LENGTH)]
    public required string Language { get; set; }

    public PlayerStatus Status { get; set; }
    public int CampaignOffsetId { get; set; }

    public GameIndex Game { get; set; } = null!;
}
