using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;

/// <summary>
/// Everything the movement rules need to know about the tile a Force is trying to enter.
/// </summary>
/// <remarks>
/// <para>
/// A flat snapshot built once by the handler, so no EF entity reaches the rule layer and a strategy never has to query anything. This mirrors
/// <c>ForceActionContext</c>, which serves the context-action pipeline the same way.
/// </para>
/// <para>
/// The derived members below are the movement rules themselves. They live here rather than in the handler because they are pure arithmetic over
/// this snapshot, which makes them testable without a database.
/// </para>
/// </remarks>
internal sealed record MovementTargetTile
{
    public required int XCoord { get; init; }

    public required int YCoord { get; init; }

    public required CampaignMapGeographyType GeographyType { get; init; }

    /// <summary>The geography display name, used to explain a refusal in terms the player recognises.</summary>
    public required string GeographyName { get; init; }

    public required decimal GeographyMovementCost { get; init; }

    /// <summary>The biome display name, used to explain a refusal in terms the player recognises.</summary>
    public required string BiomeName { get; init; }

    public required decimal BiomeMovementCost { get; init; }

    public required bool BiomeIsWater { get; init; }

    public required bool BiomeIsTraversable { get; init; }

    /// <summary>
    /// Whether this tile is water, for the purpose of deciding which movement domains may enter it.
    /// </summary>
    /// <remarks>
    /// The rule itself lives in <see cref="CampaignTileWater"/>, which gathering also uses, so a tile a land Force is refused as water can never feed it as land.
    /// </remarks>
    public bool IsWater => CampaignTileWater.IsWater(BiomeIsWater, GeographyType);

    /// <summary>
    /// Whether nothing can enter this tile at all, whatever its movement domain or how much movement it has left.
    /// </summary>
    /// <remarks>
    /// Two independent markers, matching how the seed data expresses the idea: Mountainous uses the geography's
    /// <see cref="CampaignGeography.IMPASSABLE_MOVEMENT_COST"/> sentinel, while the Void biome is simply not traversable.
    /// </remarks>
    public bool IsImpassable => !BiomeIsTraversable || GeographyMovementCost >= CampaignGeography.IMPASSABLE_MOVEMENT_COST;

    /// <summary>
    /// Whether it is the ground itself that cannot be crossed, rather than what is painted on it.
    /// </summary>
    /// <remarks>
    /// Only Mountainous carries the sentinel today, so this is what lets a refusal name the mountain instead of falling back on the general
    /// wording the Void biome has to use.
    /// </remarks>
    public bool GeographyIsImpassable => GeographyMovementCost >= CampaignGeography.IMPASSABLE_MOVEMENT_COST;

    /// <summary>
    /// What it costs to cross this tile, in movement points.
    /// </summary>
    /// <remarks>
    /// The geography is the ground and the biome is a modifier on it, so the biome contributes only its excess over the neutral cost of 1. That way
    /// ordinary Grassland over Flat stays at 1, Desert's 1.1 adds its tenth, and Hilly's 2 is not doubled merely by having a biome painted on it.
    /// Meaningless when <see cref="IsImpassable"/> is set, so check that first.
    /// </remarks>
    public decimal MovementCost => GeographyMovementCost + (BiomeMovementCost - 1m);

    /// <summary>
    /// How many Turn-ends a Force is pinned on the tile it steps onto by the cost of the step, before it holds enough movement to leave again.
    /// </summary>
    /// <param name="stepCost">
    /// What the step costs, from <see cref="IMovementDomainStrategy.GetStepCost"/>. That is the tile's <see cref="MovementCost"/> adjusted for any river the
    /// step crosses or follows, and it has to be the same figure the step is charged, or the stranding check would guard a price nobody pays.
    /// </param>
    /// <param name="allowanceRaw">The Force's full per-Turn allowance from its compiled template, raw Scale 1,000.</param>
    /// <param name="remainingRaw">What the Force is holding immediately <i>before</i> the step, raw Scale 1,000.</param>
    /// <returns>Zero when the Force can still move on after entering; otherwise the number of Turn-ends it spends standing here.</returns>
    /// <remarks>
    /// <para>
    /// Entering costs the tile's whole price and the balance is allowed to go negative, so the question worth answering is not "can this Force
    /// afford the tile?" but "how long will the tile cost it?". That is this number, and it is the single quantity the step preview and the supply
    /// projection are both meant to read, so the two cannot end up disagreeing about how long a Force will be sitting there.
    /// </para>
    /// <para>
    /// The arithmetic is <c>floor((cost - remaining) / allowance) + 1</c>, which is exactly the Turn on which
    /// <c>MovementReplenishment.ReplenishCampaignMovement</c> brings the balance back above zero: debt is uncapped and carries in full, so the Force
    /// opens each following Turn one whole allowance better off than the last.
    /// </para>
    /// <para>
    /// The comparison against the remaining points is deliberately <b>not</b> strict. A step that lands a Force on precisely zero has stranded it
    /// for a Turn just as surely as debt would, because the entry gate asks for more than nothing rather than for nothing.
    /// </para>
    /// <para>
    /// Static rather than read off the tile, because the tile's own cost is no longer the whole story: a river crossed or followed changes the price of
    /// the step without changing the tile.
    /// </para>
    /// </remarks>
    public static int TurnsHeld(decimal stepCost, long allowanceRaw, long remainingRaw)
    {
        long costRaw = ScaledAmount.FromDecimal(stepCost).RawValue;

        if (costRaw < remainingRaw)
        {
            // Something is still left over after paying, so the Force may step straight on and this tile holds it for no time at all.
            return 0;
        }

        if (allowanceRaw <= 0)
        {
            // A Force with no allowance never recovers, so the tile holds it for ever. Answering with the largest count keeps every caller's
            // comparison honest rather than making each of them special-case the division.
            return int.MaxValue;
        }

        // Both operands are non-negative by the check above, so the truncating division is the floor the rule asks for.
        long shortfallRaw = costRaw - remainingRaw;

        return (int)(shortfallRaw / allowanceRaw) + 1;
    }

    /// <summary>
    /// Names the tile for a refusal message, for example "Hilly Forest at (12, 7)".
    /// </summary>
    public string Describe()
    {
        return $"{GeographyName} {BiomeName} at ({XCoord}, {YCoord})";
    }
}
