using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// One tile of the Player-neutral campaign map: what it is, who has seen it, and who holds it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ownership is a set of nullable Player references, not a state enum.</b> The states in <c>docs/design/campaign-map/tile-ownership.md</c> - Unclaimed, Claimed, Owned,
/// Occupied, Disputed, Abandoned - are read <i>off</i> these columns rather than stored, because most combinations of parallel state enums are illegal and would have to be
/// policed. Disputed is derived from a <c>BattleEngagement</c> on the tile and is never stored at all.
/// </para>
/// <para>
/// <b>Claims live elsewhere.</b> Ownership is exclusive, so it fits on the tile; a claim is not, so it is a row in <see cref="CampaignMapClaim"/>. 
/// The two never coexist on one tile - a tile becoming owned extinguishes every rival claim on it.
/// </para>
/// <para>
/// <b>The effective holder is <c>OccupiedByPlayerId ?? OwnedByPlayerId</c>.</b> An occupier collects the yield without taking ownership, so a caller that wants whoever is
/// drawing from the tile reads the occupier first and falls back to the owner.
/// </para>
/// </remarks>
public class CampaignMap
{
    [Key]
    public int CampaignMapId { get; set; }
    public required int GameId { get; set; }

    public int XCoord { get; set; }
    public int YCoord { get; set; }

    public int? FirstSeenOnTurnId { get; set; }
    public int? FirstSeenByPlayerId { get; set; }

    public int? LastVisitedOnTurnId { get; set; }

    /// <summary>
    /// Records the last Player Id whose Force last visited this Tile.
    /// </summary>
    public int? LastVisitedByPlayerId { get; set; }

    /// <summary>
    /// The Player who owns this tile administratively, or <see langword="null"/> when nobody does.
    /// </summary>
    /// <remarks>
    /// Exclusive, and the tier that yields, construction and Settlement scoping all hang off. 
    /// A tile becomes owned through a Settlement's territory, through a structure asserting ownership, through enclosure, or by transfer from the attrition timer or conquest.
    /// </remarks>
    public int? OwnedByPlayerId { get; set; }

    /// <summary>
    /// The Settlement this tile is assigned to, or <see langword="null"/> when its ownership comes from something other than Settlement territory.
    /// </summary>
    /// <remarks>
    /// Carrying the Settlement on the tile is what lets Settlement-scoped modifiers reach it. Owned land won by a structure, by enclosure or by attrition carries none, and no
    /// Settlement-scoped modifier applies to it.
    /// <para>
    /// <b>Not a foreign key,</b> because Settlements are not modelled yet - see <c>docs/design/settlements.md</c>. Nothing writes this column until they are.
    /// </para>
    /// </remarks>
    public int? OwnedBySettlementId { get; set; }

    /// <summary>
    /// The rival Player whose visible Forces hold this tile alone, or <see langword="null"/> when none do. The owner keeps ownership throughout.
    /// </summary>
    /// <remarks>
    /// Occupation changes <i>who collects</i> the yield, never the numbers: the occupier collects and the tile regenerates at the owner's levels. 
    /// A hidden Force occupies nothing, and a Force present under an organised agreement is not occupying either.
    /// </remarks>
    public int? OccupiedByPlayerId { get; set; }

    /// <summary>
    /// The Turn the current occupation began, which is what the attrition timer counts from, or <see langword="null"/> when the tile is not occupied.
    /// </summary>
    /// <remarks>
    /// Not a foreign key, the same as <see cref="FirstSeenOnTurnId"/> and <see cref="LastVisitedOnTurnId"/> on this table: the game purge removes <c>turns.PlayerTurn</c> rows
    /// before the tiles, so a constraint here would break that ordering.
    /// </remarks>
    public int? OccupiedSinceTurnId { get; set; }

    /// <summary>
    /// The Player who owned this tile before it was abandoned, or <see langword="null"/> when it is not abandoned.
    /// </summary>
    /// <remarks>
    /// Set with <see cref="AbandonedOnTurnId"/> when a Settlement is razed, and cleared with it when the tile reverts to Unclaimed and forgets its former owner.
    /// </remarks>
    public int? LastOwnedByPlayerId { get; set; }

    /// <summary>
    /// The Turn this tile was abandoned on, or <see langword="null"/> when it is not abandoned.
    /// </summary>
    /// <remarks>
    /// An abandoned tile reverts to Unclaimed after 5 turns, at which point this and <see cref="LastOwnedByPlayerId"/> are both cleared. Not a foreign key, for the same
    /// reason as <see cref="OccupiedSinceTurnId"/>.
    /// </remarks>
    public int? AbandonedOnTurnId { get; set; }

    /// <summary>
    /// How much rebellion is left on this tile, as a percentage, or <see langword="null"/> when it is not rebelling.
    /// </summary>
    /// <remarks>
    /// Rebelling is a modifier on Owned, not a state of its own: a non-null value here means the tile yields 50% and is still in rebellion.
    /// Conquest can only ever end with the tile owned by the conqueror - this says how long that takes, never where it ends up.
    /// <para>
    /// <b>The per-tile randomness is the starting value.</b> Conquest rolls it, and the per-round pass takes it down from there, which spends the design's random 1 to 5
    /// rounds without a second column for the rate. The column is set back to <see langword="null"/> once it runs out rather than left sitting at 0, so "is rebelling" stays
    /// a null check and never a comparison - which is why 0 is not a legal stored value.
    /// </para>
    /// <para>
    /// <b>Neither the roll nor the size of the step belongs here.</b> The rebellion length is a value to be tuned, and a step that varies by round, by tile or by what is
    /// standing on it is a change to the pass and not to the schema. All this column promises is a percentage that falls.
    /// </para>
    /// <para>
    /// A countdown rather than an expiry Turn for exactly that reason: a tile with a military Force on it will not rebel, which the pass can spend as a round that subtracts
    /// nothing. Held as a whole percent, unlike <c>StructureInstance.CompletionPercent</c>, because nothing here needs sub-percent resolution.
    /// </para>
    /// </remarks>
    public int? RebellionPercentRemaining { get; set; }

    /// <summary>
    /// Whether any Player holds a claim on this tile, which is the map read's licence to skip the claim junction for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A denormalisation of <see cref="CampaignMapClaim"/>, and the only one on this table. The overwhelming majority of tiles on a map are claimed by nobody, so without it every map read
    /// would have to ask the junction about ground that has never had a row in it. With it, the question is a column already in the row the read loaded anyway.
    /// </para>
    /// <para>
    /// <b>A flag rather than a count.</b> All the read needs is whether to look, and "any or none" is the one fact the per-round pass can keep right without maintaining a running total that
    /// could drift from the rows it describes. Whose claims they are, and how many, come from the junction - which is only asked when this is <see langword="true"/>.
    /// </para>
    /// <para>
    /// Defaulted in the database as well as in C#, because game setup copies tiles with a raw <c>INSERT ... SELECT</c> that does not name this column - the same reason
    /// <see cref="SouthRiver"/> carries a default.
    /// </para>
    /// </remarks>
    public bool HasClaims { get; set; }

    public required BiomeType BiomeTypeId { get; set; }
    public required CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }

    /// <summary>
    /// The river running along this tile's South edge — the edge it shares with the tile below it. Copied from the gallery map at game setup.
    /// </summary>
    /// <remarks>
    /// A tile owns only its South and East edges, so its North edge is the <see cref="SouthRiver"/> of the tile above it. See
    /// <see cref="CampaignMapGallery.SouthRiver"/>.
    /// </remarks>
    public RiverWidth SouthRiver { get; set; }

    /// <summary>
    /// The river running along this tile's East edge — the edge it shares with the tile to its right. Copied from the gallery map at game setup.
    /// </summary>
    /// <remarks>
    /// A tile owns only its South and East edges, so its West edge is the <see cref="EastRiver"/> of the tile to its left. See
    /// <see cref="CampaignMapGallery.EastRiver"/>.
    /// </remarks>
    public RiverWidth EastRiver { get; set; }

    /// <summary>
    /// Who holds this tile right now - the occupier if there is one, otherwise the owner - or <see langword="null"/> when nobody does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The answer to "who is drawing from this tile", which is a different question from who owns it: an occupier collects the yield without taking ownership, so a caller that wants the
    /// holder has to read the occupier first and fall back to the owner. Spelling that out at each call site is how the two get out of step.
    /// </para>
    /// <para>
    /// <b>Not mapped, and not for queries.</b> EF cannot translate a property with a body, so a LINQ query reads the same rule through
    /// <see cref="EFFECTIVE_HOLDER_PROJECTION"/> instead. The two are deliberately one line each so that they cannot disagree.
    /// </para>
    /// </remarks>
    [NotMapped]
    public int? EffectiveHolderPlayerId => OccupiedByPlayerId ?? OwnedByPlayerId;

    /// <summary>
    /// <see cref="EffectiveHolderPlayerId"/> as a projection a query can run in SQL.
    /// </summary>
    /// <remarks>
    /// The database half of the helper above, for callers reading the holder off the server rather than off a materialised tile - <c>BattleEngagementService</c> among them. Used as the whole
    /// selector, <c>.Select(CampaignMap.EFFECTIVE_HOLDER_PROJECTION)</c>, since an expression tree cannot be spliced into a lambda written inline.
    /// </remarks>
    public static readonly Expression<Func<CampaignMap, int?>> EFFECTIVE_HOLDER_PROJECTION = tile => tile.OccupiedByPlayerId ?? tile.OwnedByPlayerId;

    [ForeignKey("BiomeTypeId")]
    public required CampaignBiome Biome { get; set; }

    [ForeignKey("CampaignMapGeographyTypeId")]
    public required CampaignGeography Geography { get; set; }

    [ForeignKey("GameId")]
    public required GameIndex Game { get; set; }

    [ForeignKey("FirstSeenByPlayerId")]
    public required Player? FirstVisitor { get; set; }

    [ForeignKey("LastVisitedByPlayerId")]
    public required Player? LastVisitor { get; set; }

    [ForeignKey("OwnedByPlayerId")]
    public required Player? OwnedBy { get; set; }
}
