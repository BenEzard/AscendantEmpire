using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Decides how much of each thing a Force is carrying it eats, by levelling the carried Assets down to a common number of rations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Levelled in rations rather than in Asset units.</b> What the carried Assets have in common is how long each would keep the Force alive, not how much of
/// it there is: grain at one Asset per ration and fish at two are not comparable as quantities, and levelling them as quantities would protect whichever
/// happens to be priced cheaply. Levelling in rations eats the Asset that covers the most Turns first, so a Force runs out of everything at once rather than
/// running out of its staple while a delicacy rides along untouched.
/// </para>
/// <para>
/// <b>Levelling rather than proportional</b>, where a holding worth a tenth of the food would give up a tenth of itself. The water-line arithmetic is
/// <see cref="WaterLineAllocator"/>'s, which is the same arithmetic that spreads an expense across a network of banks - the other axis the game levels on.
/// </para>
/// <para>
/// <b>Rounding favours the Force.</b> A ration take converts back to an amount of the Asset rounded up, so a need the load can cover is covered rather than
/// left a thousandth short and an individual starved for it. The cost is that a take can overshoot its ration value by a thousandth of an Asset, which is
/// why <see cref="RationTake.Fed"/> is recomputed from the amounts actually taken and capped at the need.
/// </para>
/// <para>
/// Arithmetic only: it reads no banks and writes none. <see cref="ICarriedFoodConsumptionService"/> is what gathers the holdings and spends the answer.
/// </para>
/// </remarks>
internal static class RationLeveller
{
    /// <summary>
    /// How much of each carried Asset to eat in order to cover <paramref name="need"/> as far as the load can.
    /// </summary>
    /// <param name="carried">What the Force is carrying that its design can eat. An Asset appears once, holding what every bank in reach holds between them.</param>
    /// <param name="need">The rations still wanted after the tile was stripped.</param>
    public static RationTake Level(IReadOnlyCollection<CarriedRation> carried, ScaledAmount need)
    {
        ArgumentNullException.ThrowIfNull(carried);

        if (need <= ScaledAmount.Zero)
        {
            return RationTake.None;
        }

        List<CarriedRation> inEatingOrder =
        [
            .. carried
                .Where(ration => ration.HeldRaw > 0 && ration.AmountPerUnit > 0)
                .OrderByDescending(ration => ration.InRations.RawValue)
                .ThenBy(ration => ration.AssetId)
        ];

        if (inEatingOrder.Count == 0)
        {
            return RationTake.None;
        }

        long[] rationTakes = WaterLineAllocator.Allocate([.. inEatingOrder.Select(ration => ration.InRations.RawValue)], need.RawValue);

        var takeRawByAssetId = new Dictionary<int, long>();
        long fedRaw = 0;

        for (int index = 0; index < inEatingOrder.Count; index++)
        {
            CarriedRation ration = inEatingOrder[index];
            long takeRaw = ToAssetUnitsRoundedUp(rationTakes[index], ration);

            if (takeRaw <= 0)
            {
                continue;
            }

            takeRawByAssetId[ration.AssetId] = takeRaw;
            fedRaw += (long)((Int128)takeRaw * ScaledAmount.Scale / ration.AmountPerUnit);
        }

        return new RationTake(takeRawByAssetId, ScaledAmount.FromRaw(Math.Min(need.RawValue, fedRaw)));
    }

    /// <summary>
    /// What a number of rations costs in one Asset, rounded up, and never more than the Force is carrying of it.
    /// </summary>
    private static long ToAssetUnitsRoundedUp(long rationsRaw, CarriedRation ration)
    {
        if (rationsRaw <= 0)
        {
            return 0;
        }

        long takeRaw = (long)(((Int128)rationsRaw * ration.AmountPerUnit + ScaledAmount.Scale - 1) / ScaledAmount.Scale);

        return Math.Min(ration.HeldRaw, takeRaw);
    }
}
