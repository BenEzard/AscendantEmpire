using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Settlements;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Settlements;

/// <summary>
/// One settlement in one Game - the player-named thing a settler becomes.
/// </summary>
/// <remarks>
/// <para>
/// <b>A Settlement sits on a <see cref="StructureInstance"/> rather than replacing it.</b> The structure is what stands on the tile, so it is what carries the construction
/// progress, the slot, the sight range, the ownership and the whole intelligence pipeline; this row carries only what is true of the settlement and of nothing else. Without
/// that split, every one of those six would have to be modelled a second time here. See <c>docs/design/settlements.md</c>.
/// </para>
/// <para>
/// <b>There is deliberately no owner column.</b> Who holds a settlement is <see cref="StructureInstance.OwnedByPlayerId"/>, which a capture moves through
/// <c>IStructureLifecycleWriter.CaptureAsync</c>. A second owner column here would be a second source of truth that a capture would have to remember to move as well.
/// <see cref="FoundingPlayerId"/> is a different fact and never changes: who founded it, whoever holds it now.
/// </para>
/// <para>
/// <b>Not soft-deleted.</b> Razing is recorded on the structure, as <see cref="StructureInstance.RemovedOnTurnId"/>, which is where every other part of the game already
/// asks whether something still stands. Razing a settlement is not built; when it is, it stamps the structure and this row stays.
/// </para>
/// </remarks>
public class Settlement
{
    /// <summary>The longest a player may make a settlement's name.</summary>
    /// <remarks>Short on purpose: the name is drawn as a label above the settlement on the campaign map, and a long one would cover its neighbours.</remarks>
    public const int NAME_MAX_LENGTH = 30;

    public int Id { get; set; }

    public int GameId { get; set; }

    /// <summary>
    /// The Structure standing on the tile for this Settlement. Unique - one Settlement per Structure, and one Structure per Settlement.
    /// </summary>
    public int StructureInstanceId { get; set; }

    /// <summary>
    /// The Player who founded the Settlement. Never changes, so a conquered Settlement still records who built it.
    /// </summary>
    public int FoundingPlayerId { get; set; }

    /// <summary>
    /// The player-chosen name, unique within the Game, or <see langword="null"/> for a tier that is not named.
    /// </summary>
    /// <remarks>
    /// <b>A camp has no name.</b> It is a place the settlers stopped rather than a place they founded, so the player is never asked for one and nothing invents one for
    /// them - see <c>SettlementTierRules</c> for which tiers are named. The uniqueness index is filtered to the rows that have a name, so any number of camps can stand
    /// unnamed in one Game while no two villages share a name.
    /// </remarks>
    [MaxLength(NAME_MAX_LENGTH)]
    public string? Name { get; set; }

    /// <summary>How far up the settlement ladder this one sits. Set at founding; growth would move it, and growth is not built.</summary>
    public SettlementTier Tier { get; set; }

    /// <summary>
    /// How many people live here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seeded from the founding Force's whole strength, healthy and injured alike. A settlement's health and sickness are their own mechanic and are not modelled by splitting this number.
    /// </para>
    /// <para>
    /// This is where the Civilians figure under <c>Society.Population.MyPeople</c> is to come from - see "What the figures will come from" in
    /// <c>docs/design/assets-data-model.md</c>. Nothing reads it yet.
    /// </para>
    /// </remarks>
    public int Population { get; set; }

    /// <summary>
    /// The Round the Settlement was founded in.
    /// </summary>
    /// <remarks>
    /// Copied from the founding Turn's <c>PlayerTurn.RoundNumber</c> rather than joined, for the same reason the Turn copies it from the Queue: the Round number moves on,
    /// and a founding date has to keep saying which Round it was, for ever.
    /// </remarks>
    public int FoundedOnRoundNumber { get; set; }

    /// <summary>
    /// The <c>turns.PlayerTurn</c> the Settlement was founded during.
    /// </summary>
    /// <remarks>
    /// Not a foreign key, matching the Turn stamps on <see cref="StructureInstance"/> and on the tile: the game purge removes <c>turns.PlayerTurn</c> before these rows.
    /// </remarks>
    public int FoundedOnTurnId { get; set; }

    [ForeignKey("GameId")]
    public GameIndex Game { get; set; } = null!;

    [ForeignKey("StructureInstanceId")]
    public StructureInstance StructureInstance { get; set; } = null!;

    [ForeignKey("FoundingPlayerId")]
    public Player FoundedBy { get; set; } = null!;
}
