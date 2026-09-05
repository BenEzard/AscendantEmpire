using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface IForceStub
{
    public int Id { get; }
    public RecordType RecordType { get; }

    /// <summary>
    /// The player who owns this Force — ownership, not command.
    /// </summary>
    public int OwnerPlayerId { get; init; }

    public int HealthyTroops { get; }
    public int InjuredTroops { get; }
    public int InitialStrength { get; set; }
    public int UnitTemplateId { get; }

    /// <summary>
    /// Whether this Force still has campaign movement left this turn, or <c>null</c> when the observing player does not own it.
    /// </summary>
    public bool? HasCampaignMovementRemaining { get; }

    /// <summary>
    /// The campaign movement points this Force has left this turn, or <c>null</c> when the observing player does not own it.
    /// </summary>
    /// <remarks>
    /// May be negative: a Force needs only half a tile's cost to enter it but pays the whole cost once there, so it can finish a step in debt.
    /// May also exceed <see cref="CampaignMovementPoints"/>, because unspent movement is carried into the next turn on top of the allowance.
    /// Masked for the same reason as <see cref="HasCampaignMovementRemaining"/> — how far an enemy can still move is intel the vision rules never granted.
    /// </remarks>
    public decimal? CampaignMovementRemaining { get; }

    /// <summary>
    /// This Force's full movement allowance for a turn, or <c>null</c> when the observing player does not own it.
    /// </summary>
    /// <remarks>The denominator for <see cref="CampaignMovementRemaining"/>, so the client can show what is left as a proportion of a full turn.</remarks>
    public decimal? CampaignMovementPoints { get; }

    /// <summary>
    /// The battle movement points this Force has left in its current Battle, or <c>null</c> when the observing player does not own it.
    /// </summary>
    /// <remarks>A separate pool from <see cref="CampaignMovementRemaining"/>, masked for the same reason.</remarks>
    public decimal? BattleMovementRemaining { get; }
}