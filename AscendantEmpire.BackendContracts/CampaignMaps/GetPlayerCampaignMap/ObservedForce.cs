using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public record ObservedForce
{
    public int Id { get; init; }
    public RecordType RecordType { get; init; }

    /// <summary>
    /// The player who owns this Force.
    /// </summary>
    /// <remarks>
    /// Named for ownership, not command. It was previously <c>CommanderId</c>, which was misleading even then — it has only ever been populated
    /// with the owning player's id — and becomes actively wrong once a Command carries a real commanding officer.
    /// </remarks>
    public int OwnerPlayerId { get; init; }

    public int UnitTemplateId { get; init; }

    /// <summary>
    /// The number of individual soldiers in this Force who are unwounded.
    /// </summary>
    public int HealthyTroops { get; init; }

    /// <summary>
    /// The number of individual soldiers in this Force who are wounded.
    /// </summary>
    public int InjuredTroops { get; init; }

    /// <summary>
    /// The total number of Forces when it was first created.
    /// </summary>
    public int InitialStrength { get; init; }

    /// <summary>
    /// Whether this Force still has campaign movement left this turn, or <c>null</c> when the observing player does not own it.
    /// </summary>
    /// <remarks>
    /// Masked for Forces the observer doesn't own. This projection covers every Force on a seen tile, so an unmasked value would tell the player how much
    /// movement an enemy has left — intel the vision rules never granted. The context-action pipeline refuses enemy Forces for the same reason.
    /// </remarks>
    public bool? HasCampaignMovementRemaining { get; init; }

    /// <summary>
    /// The campaign movement points this Force has left this turn, or <c>null</c> when the observing player does not own it.
    /// </summary>
    /// <remarks>
    /// Masked for the same reason as <see cref="HasCampaignMovementRemaining"/>, and more sharply so: a number tells an enemy exactly how far this Force can
    /// still travel, where the boolean only told them whether it could move at all.
    /// <para>
    /// May be negative. Entering a tile requires half its movement cost but spends all of it, so a slow Force crossing expensive ground ends the turn
    /// in debt — see <c>docs/design/units/unit-mechanics.md</c>, rule 2.
    /// </para>
    /// <para>
    /// May also exceed <see cref="CampaignMovementPoints"/>. Movement left unspent is carried into the next turn and added on top of the allowance, so
    /// the numerator the client shows can legitimately be larger than the denominator.
    /// </para>
    /// </remarks>
    public decimal? CampaignMovementRemaining { get; init; }

    /// <summary>
    /// This Force's full movement allowance for a turn, or <c>null</c> when the observing player does not own it.
    /// </summary>
    /// <remarks>Taken from the compiled template, and the denominator the client shows <see cref="CampaignMovementRemaining"/> against.</remarks>
    public decimal? CampaignMovementPoints { get; init; }

    /// <summary>
    /// The battle movement points this Force has left in its current Battle, or <c>null</c> when the observing player does not own it.
    /// </summary>
    /// <remarks>
    /// Masked for the same reason as the campaign figures. A separate pool, so a Force drawn into a Battle keeps the campaign movement it will need
    /// once the Battle releases it. Granted when the Unit is fielded and not yet replenished by anything.
    /// </remarks>
    public decimal? BattleMovementRemaining { get; init; }
}
