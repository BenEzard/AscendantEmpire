using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Settlements;

namespace AscendantEmpire.Application.Features.Settlements.Classes;

/// <summary>
/// How much generic storage each settlement tier grants: the storehouse every storage class overflows into.
/// </summary>
/// <remarks>
/// </remarks>
internal static class SettlementStorageRules
{
    /// <summary>
    /// The generic pool each tier grants, in whole units.
    /// </summary>
    private static readonly Dictionary<SettlementTier, long> _GENERIC_POOL_WHOLE_UNITS_BY_TIER = new()
    {
        [SettlementTier.Camp] = 2_000,
        [SettlementTier.Village] = 50_000,
        [SettlementTier.Hamlet] = 75_000,
        [SettlementTier.Town] = 125_000,
        [SettlementTier.City] = 200_000,
        [SettlementTier.MajorCity] = 500_000,
    };

    /// <summary>
    /// The generic pool a Settlement of this tier grants, raw scaled so it compares directly against a balance.
    /// </summary>
    /// <remarks>
    /// A <c>Try</c> rather than a throw, matching <see cref="SettlementTierRules.TryGetTerritoryRadius"/>: the caller reaches this from inside the end-of-Turn
    /// pipeline, where throwing would stop a Player's Turn from ending. Every real tier is authored, so a false here is a tier cast from a value that is not one -
    /// which the capacity service reports as the defect it is.
    /// </remarks>
    /// <returns><see langword="false"/> when no pool is authored for the tier, in which case <paramref name="rawCapacity"/> is 0.</returns>
    public static bool TryGetGenericPoolCapacity(SettlementTier tier, out long rawCapacity)
    {
        if (_GENERIC_POOL_WHOLE_UNITS_BY_TIER.TryGetValue(tier, out long wholeUnits) == false)
        {
            rawCapacity = 0;

            return false;
        }

        rawCapacity = ScaledAmount.FromWhole(wholeUnits).RawValue;

        return true;
    }
}
