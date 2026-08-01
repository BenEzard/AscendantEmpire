using AscendantEmpire.Domain.Entities.Resources;
using AscendantEmpire.Domain.Entities.Resources._not_used_yet;

namespace AscendantEmpire.Domain.Entities.Assets._not_used_yet;

/// <summary>
/// Faction-specific state for a resource:
/// unlock status, obsolescence countdown, current luxury classification, etc.
/// </summary>
public sealed class FactionResourceState
{
    public int FactionId { get; set; }
    public int ResourceId { get; set; }

    public FactionResourceAvailabilityState AvailabilityState { get; set; }

    /// <summary>
    /// Turn when this became unlocked, if ever.
    /// </summary>
    public int? UnlockedOnTurnNumber { get; set; }

    /// <summary>
    /// If the faction has started obsoleting this resource, this is the turn the countdown began.
    /// </summary>
    public int? ObsolescenceCountdownStartedOnTurnNumber { get; set; }

    /// <summary>
    /// Turn when the resource becomes actually obsolete for this faction.
    /// Null if no obsolescence scheduled.
    /// </summary>
    public bool IsObsolete { get; set; }

    /// <summary>
    /// Persisted/calculated classification for the faction right now.
    /// Recalculated from supply rules by domain logic.
    /// Never true if ResourceDefinition.NeverLuxury is true.
    /// </summary>
    public bool IsCurrentlyLuxury { get; set; }

    public Resource Resource { get; set; } = null!;
}
