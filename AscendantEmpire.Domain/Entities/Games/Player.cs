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
    /// Long enough for "#rrggbbaa", although a faction colour is always "#rrggbb" today.
    /// </summary>
    public const int FACTION_COLOUR_MAX_LENGTH = 9;

    /// <summary>
    /// Generous for "/portraits/ruler_01.png", which is all this ever holds.
    /// </summary>
    /// <remarks>
    /// Deliberately shorter than the 500 allowed for an arbitrary catalogue icon path: this column holds one of a fixed set of generated paths, not
    /// something a user types.
    /// </remarks>
    public const int RULER_PORTRAIT_PATH_MAX_LENGTH = 100;

    /// <summary>
    /// Stands in for a ruler portrait the player has not chosen yet.
    /// </summary>
    /// <remarks>
    /// The one value <see cref="RulerPortraitPath"/> may hold more than once within a game: any number of players can be undecided at the same time,
    /// while a portrait somebody has actually chosen is theirs alone. Empty rather than a path to a placeholder image, so there is no file to ship and
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

    /// <summary>
    /// Root-relative path to the portrait chosen for this ruler, e.g. "/portraits/ruler_07.png".
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
    /// same game already holds it. Unlike <see cref="FactionColour"/> there is no unique index behind that: the check lives in
    /// UpdatePlayerDefinitionHandler alone, which is enough because a portrait is cosmetic - the worst a lost race costs is two players sharing a
    /// portrait until one of them edits it, where a shared faction colour would make the map unreadable.
    /// </para>
    /// </remarks>
    [MaxLength(RULER_PORTRAIT_PATH_MAX_LENGTH)]
    public required string RulerPortraitPath { get; set; }

    /// <summary>
    /// What this faction's people call themselves as a group, e.g. "The Vashti".
    /// </summary>
    /// <remarks>
    /// Flavour text the player authors, not a key: nothing is looked up by it and two factions in the same game may name their peoples alike. Like
    /// <see cref="RulerName"/> and <see cref="FactionName"/> it is non-nullable and seeded with a placeholder, so every slot always has something to
    /// show in a listing even before anyone claims it.
    /// </remarks>
    [MaxLength(PEOPLE_NAME_MAX_LENGTH)]
    public required string PeopleName { get; set; }

    /// <summary>
    /// What a single member of this people is called, e.g. "Vashti" for one of "The Vashti".
    /// </summary>
    /// <remarks>
    /// Held separately rather than derived from <see cref="PeopleName"/> because no rule turns one into the other reliably - the singular of a
    /// player-invented name is the player's to decide.
    /// </remarks>
    [MaxLength(DEMONYM_MAX_LENGTH)]
    public required string Demonym { get; set; }

    /// <summary>
    /// The name of this people's culture, e.g. "Vashti Highland".
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="PeopleName"/>: a people and the culture they carry are named separately so that the two can diverge as a game runs on.
    /// </remarks>
    [MaxLength(CULTURE_NAME_MAX_LENGTH)]
    public required string CultureName { get; set; }

    /// <summary>
    /// The language this people speak, e.g. "Old Vashti".
    /// </summary>
    /// <remarks>
    /// A plain name, not a link to anything: there is no language catalogue, and the Language technology in the tech tree is unrelated to this field.
    /// </remarks>
    [MaxLength(LANGUAGE_MAX_LENGTH)]
    public required string Language { get; set; }

    public PlayerStatus Status { get; set; }
    public int CampaignOffsetId { get; set; }

    public GameIndex Game { get; set; } = null!;
}
