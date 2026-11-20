using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;

/// <summary>
/// How much of a MapResource a Force takes when it loads up, and what it costs them.
/// </summary>
/// <remarks>
/// <para>
/// Pure static, read by <see cref="TakeResourcesStrategy"/> for the button, by the options endpoint for what the dialog promises, and by the handler for
/// what it actually writes. One place, so the three cannot disagree about what a Force would get.
/// </para>
/// <para>
/// <b>Carrying is unlimited.</b> A Force's real capacity is meant to be its healthy individuals times the per-individual carry weight of the template it
/// was built from, and neither <c>Asset.Weight</c> nor a compiled carry weight exists yet (RS-41), so the figure here stands in for both. It is a flat rate
/// per healthy individual rather than anything the MapResourceType or the design has a say in - see <c>docs/design/asset-banks.md</c>, "Force cargo".
/// </para>
/// </remarks>
internal static class TakeResourcesRules
{
    /// <summary>
    /// Whole units of a MapResource one healthy individual carries away.
    /// </summary>
    /// <remarks>The wounded carry nothing, which is why this reads off the healthy count alone rather than the Force's strength.</remarks>
    public const int UNITS_PER_HEALTHY_INDIVIDUAL = 5;

    /// <summary>
    /// What a Force of this many healthy individuals would take if the tile held enough, as a raw fixed-point quantity.
    /// </summary>
    /// <remarks>
    /// Raw rather than whole, so it compares directly with <c>MapResourceInstance.Quantity</c>. The caller clamps it to what the instance actually holds.
    /// </remarks>
    public static long CalculateUnitsSoughtRaw(int healthyCount)
    {
        if (healthyCount <= 0)
        {
            return 0;
        }

        return healthyCount * (long)UNITS_PER_HEALTHY_INDIVIDUAL * ScaledAmount.Scale;
    }

    /// <summary>
    /// How much of an Asset a quantity of a MapResource gives, at a yield's rate.
    /// </summary>
    /// <remarks>
    /// Both figures are raw fixed-point, so the scale has to come back out of their product - the same arithmetic <c>TileFoodStockReader</c> does when it
    /// prices a patch as food. Widened to <see cref="Int128"/> for the multiply, which overflows a <see cref="long"/> well inside legitimate quantities.
    /// </remarks>
    public static long CalculateAssetAmountRaw(long unitsTakenRaw, long amountPerUnit)
    {
        if (unitsTakenRaw <= 0 || amountPerUnit <= 0)
        {
            return 0;
        }

        return (long)((Int128)unitsTakenRaw * amountPerUnit / ScaledAmount.Scale);
    }

    /// <summary>
    /// How a selection of Forces divides one MapResource between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>First come, first served, in ascending Force id.</b> Each Force takes everything it can carry before the next is offered what is left, so a large
    /// selection on a small patch leaves the later Forces with nothing. Not levelled, unlike a bank credit: levelling is for spreading one quantity fairly
    /// across stores the same Player owns, and this is several Forces racing for a patch. Ascending id rather than the order the client listed them, so the
    /// same order against the same patch always produces the same shares.
    /// </para>
    /// <para>
    /// A Force allotted nothing is still in the result, carrying a zero, because the player has to be told who came away empty-handed.
    /// </para>
    /// </remarks>
    /// <param name="takers">The Forces in the order, in any order.</param>
    /// <param name="availableRaw">What the MapResource holds, raw fixed-point.</param>
    public static IReadOnlyList<TakeResourcesShare> PlanShares(IReadOnlyCollection<TakeResourcesTaker> takers, long availableRaw)
    {
        ArgumentNullException.ThrowIfNull(takers);

        long remaining = Math.Max(0, availableRaw);
        List<TakeResourcesShare> shares = [];

        foreach (TakeResourcesTaker taker in takers.OrderBy(taker => taker.UnitInstanceId))
        {
            long share = Math.Min(remaining, CalculateUnitsSoughtRaw(taker.HealthyCount));

            shares.Add(new TakeResourcesShare(taker, share));
            remaining -= share;
        }

        return shares;
    }
}

/// <summary>
/// One Force offered a share of a MapResource: who it is, and how many of its individuals are fit to carry.
/// </summary>
/// <remarks>
/// Flat and free of EF, so the same arithmetic serves the strategy's context, the options endpoint's promise and the handler's write without any of the
/// three needing the others' view of a Force.
/// </remarks>
internal sealed record TakeResourcesTaker(int UnitInstanceId, string DisplayName, int HealthyCount);

/// <summary>
/// What one Force in a take comes away with, raw fixed-point.
/// </summary>
internal sealed record TakeResourcesShare(TakeResourcesTaker Taker, long UnitsTakenRaw);
