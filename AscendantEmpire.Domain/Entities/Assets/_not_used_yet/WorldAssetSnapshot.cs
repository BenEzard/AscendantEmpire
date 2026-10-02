using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Domain.Entities.Assets._not_used_yet;

/// <summary>
/// Optional materialized snapshot for balancing/AI/UI.
/// Lets you calculate relative value from world supply.
/// Can be recomputed each turn.
/// </summary>
public sealed class WorldAssetSnapshot
{
    public long Id { get; set; }

    public int TurnNumber { get; set; }
    public int AssetId { get; set; }

    /// <summary>
    /// Total world supply across all factions and banks at this turn.
    /// </summary>
    public long TotalWorldSupply { get; set; }

    /// <summary>
    /// Example denormalized metric.
    /// Higher when supply is lower.
    /// Formula is up to your game rules.
    /// </summary>
    public decimal RelativeValue { get; set; }

    public Asset Asset { get; set; } = null!;
}
