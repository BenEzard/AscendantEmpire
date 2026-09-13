using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// One Player's knowledge of one battle map tile: the battle map counterpart of <see cref="PlayerCampaignMap"/>.
/// </summary>
/// <remarks>
/// <para>
/// Rows are written for the Players taking part in a Battle when it commences, one per tile of the battle map it is fought on, and only if that Player has none for the tile yet - a battle
/// map is persistent, so what a Player explored in an earlier Battle on the same campaign tile is kept. Players who never fought there have no rows at all.
/// </para>
/// <para>
/// The fog model is the campaign map's (see <c>docs/fog-of-war.md</c>): <see cref="FirstSeenOnTurnId"/> is the exploration flag, and the terrain columns stay unknown until the tile is first
/// seen, at which point they are copied from <c>battle.BattleMap</c>.
/// </para>
/// <para>
/// Keyed on (<see cref="PlayerId"/>, <see cref="BattleMapId"/>) rather than on a contiguous block of application-assigned ids: the battle tile id already addresses the tile, so there is no
/// range to reserve and no per-Player offset to store.
/// </para>
/// <para>
/// No navigation properties, the same as <see cref="PlayerKnownStructure"/>: the rows are written and updated in bulk with raw SQL, and nothing reads them through an entity graph.
/// </para>
/// </remarks>
public class PlayerBattleMap
{
    /// <summary>
    /// The Player whose view this is. Half of the primary key.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// The <c>battle.BattleMap</c> tile this row describes. The other half of the primary key.
    /// </summary>
    public int BattleMapId { get; set; }

    /// <summary>
    /// The Game the battle map belongs to.
    /// </summary>
    /// <remarks>
    /// Carried here because <c>battle.BattleMapIndex</c> has no GameId of its own, so without it the game delete would have to reach these rows through the campaign map.
    /// </remarks>
    public int GameId { get; set; }

    /// <summary>
    /// The battle map the tile belongs to, so a whole map's worth of one Player's rows can be read or updated without joining through the tiles.
    /// </summary>
    public int BattleMapIndexId { get; set; }

    /// <summary>X position within the battle map (0-based), copied from the tile.</summary>
    public int XCoord { get; set; }

    /// <summary>Y position within the battle map (0-based), copied from the tile.</summary>
    public int YCoord { get; set; }

    /// <summary>
    /// The tile's biome as this Player knows it.
    /// </summary>
    /// <remarks>
    /// <see cref="BiomeType.Unknown"/> until the tile is first seen.
    /// </remarks>
    public BiomeType BiomeTypeId { get; set; }

    /// <summary>
    /// The tile's altitude as this Player knows it, or <see langword="null"/> until the tile is first seen.
    /// </summary>
    /// <remarks>
    /// Nullable rather than defaulted, because <see cref="BattleMapAltitude"/> has no Unknown member and 0 is <see cref="BattleMapAltitude.Ground"/> - a real altitude, not an absence of one.
    /// </remarks>
    public int? AltitudeLevel { get; set; }

    /// <summary>
    /// The <c>turns.PlayerTurn</c> in which this Player first saw the tile; <see langword="null"/> until then.
    /// </summary>
    /// <remarks>
    /// This is <b>IsExplored</b>. Once set it is never cleared or moved.
    /// </remarks>
    public int? FirstSeenOnTurnId { get; set; }

    /// <summary>
    /// The <c>turns.PlayerTurn</c> of the most recent sighting, stamped every time the tile is seen again.
    /// </summary>
    public int? LastSeenOnTurnId { get; set; }
}
