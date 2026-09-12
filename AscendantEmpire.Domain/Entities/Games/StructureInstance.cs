using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums.Structures;

using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// This table contains the WORLD view (source-of-truth) of Structures.
/// </summary>
/// <remarks>
/// <para>
/// Ownership is <see cref="OwnedByPlayerId"/>, which a capture changes; it is not derived from who controls the tile.
/// </para>
/// <para>
/// <b>Founding, construction, capture and razing go through <c>IStructureLifecycleWriter</c></b>, which records each one in <c>StructureHistory</c>. The properties those change are init-only, so a
/// tracked Structure cannot be changed any other way without a compile error. See <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// <b>Removal is a soft delete.</b> Razing stamps <see cref="RemovedOnTurnId"/> and the row stays, so the history and known-structure rows pointing at it keep a real foreign key.
/// </para>
/// </remarks>
public class StructureInstance
{
    public int Id { get; set; }

    public int GameId { get; set; }

    public int CampaignMapTileId { get; set; }

    /// <summary>
    /// Will be null for Structures on the Campaign Map, and populated on the Battle Map.
    /// </summary>
    public int? BattleMapTileId { get; set; }

    public int StructureId { get; set; }

    /// <summary>
    /// The type of slot this Structure occupies on the Tile.
    /// </summary>
    /// <remarks>For BattleMaps will be Primary</remarks>
    public TileSlotType SlotType { get; set; }

    /// <summary>
    /// The position of the Structure on the Tile.
    /// </summary>
    /// <remarks>For BattleMaps will be WholeTile</remarks>
    public PositionOnTile Position { get; set; }

    /// <summary>
    /// Who holds the Structure now.
    /// </summary>
    /// <remarks>
    /// Init-only: a capture changes it through <c>IStructureLifecycleWriter.CaptureAsync</c>, which records the change as it makes it.
    /// </remarks>
    public int OwnedByPlayerId { get; init; }

    /// <summary>
    /// The <c>PlayerTurn</c> the Structure was founded during, or 0 for one placed outside any Turn.
    /// </summary>
    /// <remarks>
    /// Not a foreign key. The column predates <c>turns.PlayerTurn</c>, and 0 is the same "no Turn" sentinel the campaign map's exploration stamp uses.
    /// </remarks>
    public int CreatedOnTurnId { get; set; }

    /// <summary>
    /// How far built the Structure is, from 0 to 100.
    /// </summary>
    /// <remarks>
    /// Init-only: construction advances it through <c>IStructureLifecycleWriter.ProgressAsync</c>.
    /// </remarks>
    public double CompletionPercent { get; init; }

    /// <summary>
    /// The <c>PlayerTurn</c> construction finished during, or null while it is still being built.
    /// </summary>
    /// <remarks>
    /// Init-only, and not a foreign key, for the same reasons as <see cref="CompletionPercent"/> and <see cref="CreatedOnTurnId"/>.
    /// </remarks>
    public int? CompletedOnTurnId { get; init; }

    /// <summary>
    /// The <c>PlayerTurn</c> the Structure was razed during, or null while it stands. The soft delete: a razed Structure's row is never removed.
    /// </summary>
    /// <remarks>
    /// Init-only: razing stamps it through <c>IStructureLifecycleWriter.RazeAsync</c>. Not a foreign key, for the same reason as <see cref="CreatedOnTurnId"/>.
    /// </remarks>
    public int? RemovedOnTurnId { get; init; }

    [ForeignKey("GameId")]
    public GameIndex Game { get; set; } = null!;

    [ForeignKey("StructureId")]
    public Structure Structure { get; set; } = null!;

    [ForeignKey("CampaignMapTileId")]
    public CampaignMap CampaignMap { get; set; } = null!;

    [ForeignKey("OwnedByPlayerId")]
    public Player OwnedBy { get; set; } = null!;
}
