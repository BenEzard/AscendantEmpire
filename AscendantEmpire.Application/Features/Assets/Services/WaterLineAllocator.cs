namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Spreads a take across a set of levels by levelling: the fullest are cut down to a common water-line and the rest are left alone.
/// </summary>
/// <remarks>
/// <para>
/// Chosen over a proportional spread, where a holding of a tenth of the stock would pay a tenth of the cost. Levelling takes from the richest first, so a
/// small holding keeps what it has.
/// </para>
/// <para>
/// With levels of 50, 30 and 4 and a take of 40, the water-line falls at 20: the first two are cut to 20 each and the third is untouched. A take of 84 out of
/// the same levels is the whole of it.
/// </para>
/// <para>
/// <b>The arithmetic only, over plain integers</b>, because the game levels on two different axes with it: one Asset across the banks holding it, where a
/// level is a balance's quantity, and a Force's carried Assets against each other, where a level is what one of them is worth in rations. Both want the same
/// water-line and the same tie-breaking, and neither wants the other's units.
/// </para>
/// </remarks>
internal static class WaterLineAllocator
{
    /// <summary>
    /// How much to deduct from each level, parallel to <paramref name="levels"/>.
    /// </summary>
    /// <remarks>
    /// <b>The leftover units go richest-first.</b> A level is an integer, so a water-line that divides exactly is the exception and a few units are normally
    /// left over. They come off the levels standing on the line in the order given, which is what makes the same take against the same levels always deduct
    /// the same amounts.
    /// </remarks>
    /// <param name="levels">The levels to take from, in the order ties are to be broken - richest first, by the caller's own ordering.</param>
    /// <param name="take">How much to take. At or above what the levels hold between them, every one of them is emptied.</param>
    public static long[] Allocate(IReadOnlyList<long> levels, long take)
    {
        ArgumentNullException.ThrowIfNull(levels);

        long[] deductions = new long[levels.Count];

        if (take <= 0)
        {
            return deductions;
        }

        if (take >= levels.Sum())
        {
            return [.. levels];
        }

        long waterLine = FindWaterLine(levels, take);

        // Everything strictly above the line comes off first. The line is the highest one that still yields enough, so what is left outstanding after this
        // pass is fewer units than there are levels standing on the line - never more.
        long outstanding = take;

        for (int index = 0; index < levels.Count; index++)
        {
            long aboveLine = Math.Max(0, levels[index] - (waterLine + 1));

            deductions[index] = aboveLine;
            outstanding -= aboveLine;
        }

        // The remainder is one unit each off the levels standing on the line, richest first.
        for (int index = 0; index < levels.Count && outstanding > 0; index++)
        {
            if (levels[index] - deductions[index] <= waterLine)
            {
                continue;
            }

            deductions[index]++;
            outstanding--;
        }

        return deductions;
    }

    /// <summary>
    /// The highest level at which cutting every one of them down to it would still yield at least what is being taken.
    /// </summary>
    /// <remarks>
    /// Binary searched rather than walked, because a level can sit anywhere in a range of billions. What sits above a level only falls as the level rises,
    /// which is what makes the search sound: at zero it is everything the levels hold, which is more than is being taken, and at the fullest level it is
    /// nothing, so the line lies between the two and the search cannot run off either end.
    /// </remarks>
    private static long FindWaterLine(IReadOnlyList<long> levels, long take)
    {
        long low = 0;
        long high = levels.Max();

        while (low < high)
        {
            // Rounded up, so a two-wide range tries the upper candidate and the loop cannot sit still on the lower one.
            long candidate = low + ((high - low + 1) / 2);

            if (SumAboveLevel(levels, candidate) >= take)
            {
                low = candidate;
            }
            else
            {
                high = candidate - 1;
            }
        }

        return low;
    }

    /// <summary>
    /// How much the levels hold above the given line, which is what cutting every one of them down to it would yield.
    /// </summary>
    private static long SumAboveLevel(IReadOnlyList<long> levels, long line)
    {
        return levels.Sum(level => Math.Max(0, level - line));
    }
}
