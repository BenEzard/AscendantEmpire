using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Units.Services;

/// <summary>
/// Works out what a Force loses to a Turn without enough food, and which of its troops the loss comes out of.
/// </summary>
/// <remarks>
/// <para>
/// Pure, and deliberately so: the whole model is arithmetic over the Force's troop counts plus a single roll, which makes it testable with no database at
/// all and leaves <see cref="StarvationUnderfedForceResolver"/> with only the reading and writing to do.
/// </para>
/// <para>
/// Every step is integer arithmetic in thousandths, never floating point. A casualty count has to come out identically on every machine and on every re-run of
/// the end-of-Turn pipeline, and a <see langword="double"/> does not promise that.
/// </para>
/// <para>
/// See <c>docs/design/units/unit-support-and-supply.md</c> for the rule this implements.
/// </para>
/// </remarks>
internal static class StarvationCasualtyCalculator
{
    /// <summary>
    /// The share of a Force lost in a Turn it got no food at all, per thousand. A Force that went only part-way short loses this in proportion.
    /// </summary>
    /// <remarks>
    /// The single tuning knob for how harsh starvation is, and deliberately a constant rather than a roll. The total has to stay proportional to the lack of
    /// food - that is the whole mechanic - and the tile toolbar's support forecast is only worth reading if a Player can turn "this tile will not feed me"
    /// into "and here is what it will cost me". The chance belongs in <see cref="_MIN_DEAD_SHARE_PER_THOUSAND"/>, on who dies rather than on how many suffer.
    /// </remarks>
    private const long _STARVATION_SEVERITY_PER_THOUSAND = 340;

    /// <summary>The least of a Turn's casualties that die rather than being wounded, per thousand.</summary>
    private const int _MIN_DEAD_SHARE_PER_THOUSAND = 100;

    /// <summary>The most of a Turn's casualties that die rather than being wounded, per thousand.</summary>
    private const int _MAX_DEAD_SHARE_PER_THOUSAND = 300;

    /// <summary>
    /// The least a Force may go short by, as a share of what it needed, before the shortfall is worth a casualty at all.
    /// </summary>
    /// <remarks>
    /// A share of the need rather than a count of rations. It scales with appetite, where an absolute floor would be one more figure a large Force could sit
    /// permanently beneath; and <c>ShortfallAllocator</c> can leave a Force sharing a tile short by a thousandth of a ration, which must not cost a man a Turn.
    /// </remarks>
    private const long _MINIMUM_SHORTFALL_SHARE_PER_THOUSAND = 10;

    /// <summary>Parts per thousand, the unit both shares above are expressed in.</summary>
    private const long _PER_THOUSAND = 1_000;

    /// <summary>
    /// How many of a Force's troops a Turn's shortfall kills and wounds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The total is <c>troops x (shortfall / need) x severity</c>, <b>floored</b>. Because <c>need</c> is itself the troops times the ration one individual eats, the troops divide straight back
    /// out: what is left is the severity applied to the <b>absolute</b> shortfall, and the size of the Force decides nothing at all. A deficit of under about three rations therefore floors to
    /// nobody whether it is a scouting band or an army going without.
    /// </para>
    /// <para>
    /// Left at that, the floor makes starvation survivable for ever. A Force shrinks only until its need comes within those three rations of what the tile yields and then holds there, and on a
    /// tile yielding nothing at all a couple of individuals live indefinitely - so <c>HealthyCount + InjuredCount = 0</c> was unreachable, and the destruction path downstream could never fire.
    /// A shortfall of at least <see cref="_MINIMUM_SHORTFALL_SHARE_PER_THOUSAND"/> of the need therefore costs <b>at least one</b> casualty, which is what lets a Force starve to death. Small
    /// shortfalls are still meant to be survivable, and they are: the minimum is one casualty rather than one death, so a Force with healthy men to wound wastes away into the injured for a long
    /// while before it starts losing anybody.
    /// </para>
    /// <para>
    /// The split between the two is the one thing chance decides, on a single draw from <paramref name="random"/>. It is floored as well, and the wounded
    /// take the remainder, so the rounding error always favours the Player.
    /// </para>
    /// </remarks>
    /// <param name="troops">The Force's troops, healthy and injured together, before the shortfall is applied.</param>
    /// <param name="need">The food the Force needed. Nothing is lost when this is zero or less.</param>
    /// <param name="shortfall">The food it went without. Treated as capped at <paramref name="need"/>, which is how gathering allocates it.</param>
    /// <param name="random">The Force's own stream for this Round, from <c>GameRandom</c>. Exactly one value is drawn from it.</param>
    public static StarvationCasualties Calculate(int troops, ScaledAmount need, ScaledAmount shortfall, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (troops <= 0 || need.RawValue <= 0 || shortfall.RawValue <= 0)
        {
            return StarvationCasualties.None;
        }

        // A Force cannot go more than wholly without, whatever it was handed. Allocation already guarantees it; clamping here means the arithmetic below
        // cannot produce a casualty count above the severity however that changes.
        long shortfallRaw = Math.Min(shortfall.RawValue, need.RawValue);

        // Int128 throughout the multiply: troops in the millions against a shortfall in thousandths overflows a long well before the division brings it back
        // down to a casualty count.
        Int128 lostExactly = (Int128)troops * shortfallRaw * _STARVATION_SEVERITY_PER_THOUSAND;
        Int128 divisor = (Int128)need.RawValue * _PER_THOUSAND;

        // Integer division truncates towards zero, and neither side is ever negative here, so this is the floor.
        int casualties = (int)(lostExactly / divisor);

        if (casualties == 0 && IsWorthACasualty(shortfallRaw, need))
        {
            casualties = 1;
        }

        casualties = Math.Min(casualties, troops);

        if (casualties == 0)
        {
            return StarvationCasualties.None;
        }

        // The one roll. Inclusive of both ends, hence the + 1 - Random.Next's upper bound is exclusive.
        int deadSharePerThousand = random.Next(_MIN_DEAD_SHARE_PER_THOUSAND, _MAX_DEAD_SHARE_PER_THOUSAND + 1);

        int killed = (int)((long)casualties * deadSharePerThousand / _PER_THOUSAND);

        return new StarvationCasualties(killed, casualties - killed);
    }

    /// <summary>
    /// Whether a shortfall the proportional total rounded down to nobody is nonetheless big enough to cost a casualty.
    /// </summary>
    /// <remarks>
    /// Cross-multiplied rather than divided into a share first, because a share taken by integer division would read anything under a thousandth of the need
    /// as nothing and let the threshold be undercut. Int128 for the same reason the total above needs it: a need in thousandths against the scale overflows a
    /// <see langword="long"/> at troop counts the game allows.
    /// </remarks>
    private static bool IsWorthACasualty(long shortfallRaw, ScaledAmount need)
    {
        return (Int128)shortfallRaw * _PER_THOUSAND >= (Int128)need.RawValue * _MINIMUM_SHORTFALL_SHARE_PER_THOUSAND;
    }

    /// <summary>
    /// The Force's troop counts after <paramref name="casualties"/> have been taken out of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deaths come off the already-wounded first and reach the healthy only once the wounded are gone, because the men least able to survive another Turn
    /// without food are the ones already down. Wounds are taken from whoever is still healthy.
    /// </para>
    /// <para>
    /// A wound with no healthy man left to take <b>kills instead</b>. There is nobody left to make worse, and a Force that is already wholly wounded is
    /// precisely the one starvation should be killing outright rather than leaving to sit there indefinitely. Between this and the line above, a Force left
    /// hungry accumulates wounded and then begins losing men, with no special case anywhere to say so.
    /// </para>
    /// </remarks>
    /// <param name="healthyCount">The Force's unwounded troops.</param>
    /// <param name="injuredCount">The Force's wounded troops.</param>
    /// <param name="casualties">What the Turn cost it, from <see cref="Calculate"/>.</param>
    /// <returns>The counts to write back. Both are zero when the Force has been wiped out.</returns>
    public static (int HealthyCount, int InjuredCount) Apply(int healthyCount, int injuredCount, StarvationCasualties casualties)
    {
        int healthy = healthyCount;
        int injured = injuredCount;

        int killedFromInjured = Math.Min(casualties.Killed, injured);
        injured -= killedFromInjured;

        int killedFromHealthy = Math.Min(casualties.Killed - killedFromInjured, healthy);
        healthy -= killedFromHealthy;

        int wounded = Math.Min(casualties.Wounded, healthy);
        healthy -= wounded;
        injured += wounded;

        // The wounds that found nobody healthy left to wound.
        int woundsWithNobodyToTake = casualties.Wounded - wounded;

        injured -= Math.Min(woundsWithNobodyToTake, injured);

        return (healthy, injured);
    }
}
