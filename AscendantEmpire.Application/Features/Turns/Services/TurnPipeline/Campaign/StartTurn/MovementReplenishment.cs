using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;

/// <summary>
/// The start-of-turn Campaign movement rule: a Force is given a full allowance, plus whatever it carried over.
/// </summary>
/// <remarks>
/// Kept as a pure static so the whole turn-by-turn table can be pinned without standing up a database, and so exactly one implementation of the rule runs in both the tests and production.
/// </remarks>
internal static class MovementReplenishment
{
    /// <summary>
    /// The most unspent movement a Force may carry into its next Turn — one whole Movement Point.
    /// </summary>
    /// <remarks>
    /// Expressed as the scale because one whole Movement Point <i>is</i> one scale unit under the Scale 1,000 convention, so the cap follows the convention if it ever moves. 
    /// This is a game rule that happens to coincide with the scale, not a scaling factor in its own right.
    /// </remarks>
    private const long _MAXIMUM_CARRY_RAW = ScaledAmount.Scale;

    /// <summary>
    /// Works out what a Force's campaign movement becomes when its owning Player's Turn opens.
    /// </summary>
    /// <param name="allowanceRaw">The Force's full per-Turn allowance from its compiled template, raw Scale 1,000 and deliberately not floored.</param>
    /// <param name="leftoverRaw">What the Force is holding right now, raw Scale 1,000. May be negative.</param>
    /// <returns>The allowance plus whatever carry the rule permits, raw Scale 1,000.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>
    /// A leftover at or above the allowance carries <b>nothing</b>. A Force that skipped its move, or is already holding a full allowance, resets to the plain allowance rather than compounding Turn on Turn.
    /// </item>
    /// <item>A leftover above one whole point but below the allowance carries exactly one point, which is rule 3's cap.</item>
    /// <item>
    /// Anything else carries in full. That covers a fractional remainder — which is what turns a 2.2 allowance into an extra move every fifth turn —
    /// and it covers <b>debt</b>, which is uncapped so that a Force which overspent pays it back out of the next turn rather than having it forgiven.
    /// </item>
    /// </list>
    /// <para>
    /// A leftover of exactly one whole point falls into the last branch and carries whole, which is the same answer the cap would give, so the strict
    /// comparison is not a boundary the caller has to reason about.
    /// </para>
    /// </remarks>
    internal static long ReplenishCampaignMovement(long allowanceRaw, long leftoverRaw)
    {
        long carryRaw;

        if (leftoverRaw >= allowanceRaw)
        {
            carryRaw = 0;
        }
        else if (leftoverRaw > _MAXIMUM_CARRY_RAW)
        {
            carryRaw = _MAXIMUM_CARRY_RAW;
        }
        else
        {
            carryRaw = leftoverRaw;
        }

        return allowanceRaw + carryRaw;
    }
}
