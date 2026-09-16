namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Splits a whole number of raw units across keys in proportion to their weights, so the parts always sum exactly to the total.
/// </summary>
/// <remarks>
/// <para>
/// The largest remainder method: every key first gets the whole units of its exact share, rounded down, and the units that rounding left over go one each to the keys whose
/// shares lost the most. Ties go to the lowest key, so the result depends only on the inputs and never on the order they were supplied in.
/// </para>
/// <para>
/// Used by gathering twice over - splitting a tile's food need across its resources, and a tile's shortfall across its Forces - with the amounts as
/// <c>ScaledAmount</c> raw values, so the "units" are thousandths.
/// </para>
/// </remarks>
internal static class ProportionalSplitter
{
    /// <summary>
    /// Splits <paramref name="total"/> across the keys of <paramref name="weights"/> in proportion to each weight.
    /// </summary>
    /// <remarks>
    /// A key's part never exceeds its exact share rounded up, so when <paramref name="total"/> is no more than the sum of the weights, no key is given more than its own
    /// weight. Keys with a weight of 0 get 0.
    /// </remarks>
    /// <param name="total">The raw units to hand out. Must not be negative.</param>
    /// <param name="weights">The weight of each key. None may be negative.</param>
    /// <returns>Every key's part, summing exactly to <paramref name="total"/>; or all zeros when every weight is 0.</returns>
    public static Dictionary<TKey, long> Split<TKey>(long total, IReadOnlyDictionary<TKey, long> weights)
        where TKey : notnull
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);

        if (weights.Values.Any(weight => weight < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(weights), "A weight cannot be negative.");
        }

        var parts = weights.Keys.ToDictionary(key => key, _ => 0L);

        // Int128 throughout: a total and a weight in the hundreds of millions of thousandths would overflow a long when multiplied.
        Int128 weightSum = weights.Values.Aggregate(Int128.Zero, (sum, weight) => sum + weight);

        // Nothing to split in proportion to, so nothing is handed out.
        if (weightSum == 0 || total == 0)
        {
            return parts;
        }

        var remainders = new List<(TKey Key, Int128 Remainder)>();
        long handedOut = 0;

        foreach ((TKey key, long weight) in weights)
        {
            // The exact share is total × weight / weightSum; the quotient is its whole part and the remainder what rounding down lost.
            Int128 product = (Int128)total * weight;
            long wholePart = (long)(product / weightSum);

            parts[key] = wholePart;
            handedOut += wholePart;
            remainders.Add((key, product % weightSum));
        }

        // Each key lost less than one unit to rounding, so fewer units are left over than there are keys, and each key receives at most one of them.
        long leftOver = total - handedOut;

        IEnumerable<TKey> largestRemaindersFirst = remainders
            .Where(entry => entry.Remainder > 0)
            .OrderByDescending(entry => entry.Remainder)
            .ThenBy(entry => entry.Key, Comparer<TKey>.Default)
            .Select(entry => entry.Key)
            .Take((int)leftOver);

        foreach (TKey key in largestRemaindersFirst)
        {
            parts[key]++;
        }

        return parts;
    }
}
