using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Assets._not_used_yet;

namespace AscendantEmpire.Domain.Entities.Assets._not_used_yet;

/// <summary>
/// Faction-specific state for an asset:
/// unlock status, obsolescence countdown, current luxury classification, etc.
/// </summary>
public sealed class FactionAssetState
{
    public int FactionId { get; set; }
    public int AssetId { get; set; }

    public FactionAssetAvailabilityState AvailabilityState { get; set; }

    /// <summary>
    /// Turn when this became unlocked, if ever.
    /// </summary>
    public int? UnlockedOnTurnNumber { get; set; }

    /// <summary>
    /// If the faction has started obsoleting this asset, this is the turn the countdown began.
    /// </summary>
    public int? ObsolescenceCountdownStartedOnTurnNumber { get; set; }

    /// <summary>
    /// Turn when the asset becomes actually obsolete for this faction.
    /// Null if no obsolescence scheduled.
    /// </summary>
    public bool IsObsolete { get; set; }

    /// <summary>
    /// Persisted/calculated classification for the faction right now.
    /// Recalculated from supply rules by domain logic.
    /// Never true if AssetDefinition.NeverLuxury is true.
    /// </summary>
    public bool IsCurrentlyLuxury { get; set; }

    public Asset Asset { get; set; } = null!;
}
