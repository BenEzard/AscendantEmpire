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
    public bool? HasMovementRemaining { get; }

    /// <summary>
    /// The campaign movement points this Force has left this turn, or <c>null</c> when the observing player does not own it.
    /// </summary>
    /// <remarks>
    /// May be negative: a Force needs only half a tile's cost to enter it but pays the whole cost once there, so it can finish a step in debt.
    /// Masked for the same reason as <see cref="HasMovementRemaining"/> — how far an enemy can still move is intel the vision rules never granted.
    /// </remarks>
    public decimal? MovementPointsRemaining { get; }

    /// <summary>
    /// This Force's full movement allowance for a turn, or <c>null</c> when the observing player does not own it.
    /// </summary>
    /// <remarks>The denominator for <see cref="MovementPointsRemaining"/>, so the client can show what is left as a proportion of a full turn.</remarks>
    public decimal? CampaignMovementPoints { get; }
}