using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Takes an amount of one Asset out of the balances holding it, levelling them: the fullest are cut down to a common water-line and the rest are left alone.
/// </summary>
/// <remarks>
/// <para>
/// Shared by everything that debits a set of banks at once - an expense spread across a network, and a Force eating what its network is carrying - so the
/// same take against the same banks always spends the same rows. The arithmetic is <see cref="WaterLineAllocator"/>'s; what is here is the ordering and the
/// write.
/// </para>
/// <para>
/// <b>Only <see cref="AssetBankBalance.Quantity"/> is touched.</b> A strategic reserve is out of circulation until it is released, and nothing that levels is
/// allowed to reach one.
/// </para>
/// <para>
/// <b>Stages, never saves</b>, and never removes an emptied row: whether a row left holding nothing should go is the caller's business, since only it knows
/// whether it is finished with the bank.
/// </para>
/// </remarks>
internal static class BalanceLeveller
{
    /// <summary>
    /// Takes <paramref name="takeRaw"/> out of the balances between them, and answers what it took.
    /// </summary>
    /// <remarks>
    /// A take at or above what the balances hold empties every one of them, which is the whole of what is there and needs no levelling.
    /// </remarks>
    /// <param name="held">The balances to take from, all of one Asset, tracked by the caller's context.</param>
    /// <param name="takeRaw">How much to take, in raw scaled units.</param>
    public static long Take(IReadOnlyList<AssetBankBalance> held, long takeRaw)
    {
        ArgumentNullException.ThrowIfNull(held);

        if (takeRaw <= 0)
        {
            return 0;
        }

        long total = held.Sum(balance => balance.Quantity);

        if (takeRaw >= total)
        {
            foreach (AssetBankBalance balance in held)
            {
                balance.Quantity = 0;
            }

            return total;
        }

        List<AssetBankBalance> inSpendOrder =
        [
            .. held
                .OrderByDescending(balance => balance.Quantity)
                .ThenBy(balance => balance.AssetBankId)
        ];

        long[] deductions = WaterLineAllocator.Allocate([.. inSpendOrder.Select(balance => balance.Quantity)], takeRaw);

        for (int index = 0; index < inSpendOrder.Count; index++)
        {
            inSpendOrder[index].Quantity -= deductions[index];
        }

        return takeRaw;
    }
}
