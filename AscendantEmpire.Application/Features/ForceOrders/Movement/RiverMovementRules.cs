using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement;

/// <summary>
/// What the rivers around a step have to say about it: which river it crosses, and which it runs alongside.
/// </summary>
/// <remarks>
/// Pure geometry. It says nothing about whether the step is allowed or what it costs, because that depends on how the Force moves: a Land Force pays to
/// cross, a ship sails up a Large river, and an Air Force ignores rivers entirely. Each <see cref="IMovementDomainStrategy"/> makes that call.
/// </remarks>
/// <param name="Crossed">The river the step crosses from one bank to the other, or <see cref="RiverWidth.None"/>.</param>
/// <param name="Along">
/// The river the step runs alongside, with both tiles on the same bank, or <see cref="RiverWidth.None"/>. Only orthogonal steps can run along a river.
/// </param>
/// <param name="TargetBank">
/// The widest river on the target tile's edges that run parallel to the step. This is what a ship entering from the sea needs to see: a river mouth
/// meeting the coast. Orthogonal steps only.
/// </param>
internal readonly record struct RiverStepEffect(RiverWidth Crossed, RiverWidth Along, RiverWidth TargetBank)
{
    /// <summary>A step with no river anywhere near it.</summary>
    internal static RiverStepEffect None => new(RiverWidth.None, RiverWidth.None, RiverWidth.None);
}

/// <summary>
/// The river rules for a single campaign step: the geometry of crossing and following a river, and the costs that go with them.
/// </summary>
/// <remarks>
/// <para>
/// Rivers run between tiles. A step <b>crosses</b> a river when it goes from one bank to the other, and runs <b>along</b> one when both tiles sit on
/// the same bank of a continuous river parallel to the step. The rules are in <c>docs/design/units/unit-movement.md</c>; this is their single
/// implementation.
/// </para>
/// <para>
/// Deliberately kept apart from <see cref="MovementEntryRules"/>. That class answers a question about a <i>tile</i>, and fog of war shares it; a river
/// belongs to the <i>step</i>. A tile across a Large river can still be entered from another side, so it must not be revealed as unenterable.
/// </para>
/// </remarks>
internal static class RiverMovementRules
{
    /// <summary>What a Land Force pays, on top of the tile's cost, to cross a Medium river.</summary>
    public const decimal MEDIUM_RIVER_CROSSING_COST = 1m;

    /// <summary>How much cheaper a Land step becomes when it runs along a Medium river.</summary>
    public const decimal MEDIUM_RIVER_ALONG_REDUCTION = 0.25m;

    /// <summary>How much cheaper a Land step becomes when it runs along a Large river.</summary>
    public const decimal LARGE_RIVER_ALONG_REDUCTION = 0.5m;

    /// <summary>The least any Land step can cost once a river has made it cheaper, so no river can make movement free.</summary>
    public const decimal MINIMUM_STEP_COST = 0.25m;

    /// <summary>The flat cost of a ship's step onto a bank tile, whatever the land beside the river is like.</summary>
    public const decimal RIVER_SAILING_COST = 0.5m;

    /// <summary>
    /// Works out which rivers a step in <paramref name="direction"/> from the origin crosses and runs alongside.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Orthogonal steps</b> cross the edge the two tiles share. They run along a river when, on one side, both tiles have a river on their edge
    /// parallel to the step. That run is only as wide as its narrower half, and when both sides qualify the wider side wins.
    /// </para>
    /// <para>
    /// <b>Diagonal steps</b> pass through a corner. The river only separates the two tiles when both ways round the corner cross it, so a river that
    /// merely ends at the corner is never crossed. When both ways cross, the Force takes the easier one. A diagonal step never runs along a river.
    /// </para>
    /// </remarks>
    internal static RiverStepEffect Evaluate(RiverEdges edges, MoveDirection direction)
    {
        ArgumentNullException.ThrowIfNull(edges);

        (int deltaX, int deltaY) = MapDirections.GetDelta(direction);

        if (deltaX != 0 && deltaY != 0)
        {
            return new RiverStepEffect(EvaluateDiagonalCrossing(edges, deltaX, deltaY), RiverWidth.None, RiverWidth.None);
        }

        RiverWidth crossed = edges.GetEdge(0, 0, deltaX, deltaY);
        RiverWidth along = RiverWidth.None;
        RiverWidth targetBank = RiverWidth.None;

        // The two sides of the step: north and south of an east-west step, west and east of a north-south one.
        foreach (int side in (int[])[-1, 1])
        {
            (int sideX, int sideY) = deltaX == 0 ? (side, 0) : (0, side);

            RiverWidth atOrigin = edges.GetEdge(0, 0, sideX, sideY);
            RiverWidth atTarget = edges.GetEdge(deltaX, deltaY, deltaX + sideX, deltaY + sideY);

            // A river on only one of the two tiles' edges ends or turns between them, so it is not followed. Narrower of the two gives None for that.
            along = Wider(along, Narrower(atOrigin, atTarget));
            targetBank = Wider(targetBank, atTarget);
        }

        return new RiverStepEffect(crossed, along, targetBank);
    }

    /// <summary>
    /// What a Land step costs once the rivers it crosses and follows have been taken into account.
    /// </summary>
    /// <remarks>
    /// A step can both cross a river and run along another, such as crossing a tributary beside a main river, and then both apply. A Large crossing adds
    /// nothing here, because the Land strategy refuses it before any cost is asked for.
    /// </remarks>
    internal static decimal GetLandStepCost(MovementStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        decimal crossingCost = step.Rivers.Crossed == RiverWidth.Medium ? MEDIUM_RIVER_CROSSING_COST : 0m;

        decimal alongReduction = step.Rivers.Along switch
        {
            RiverWidth.Medium => MEDIUM_RIVER_ALONG_REDUCTION,
            RiverWidth.Large => LARGE_RIVER_ALONG_REDUCTION,
            _ => 0m
        };

        return Math.Max(MINIMUM_STEP_COST, step.Target.MovementCost + crossingCost - alongReduction);
    }

    /// <summary>
    /// Judges a Water or BelowWater step, which may leave open water only to sail up a Large river.
    /// </summary>
    /// <remarks>
    /// <para>A ship may stand on a <b>bank tile</b> (a land tile with a Large river on one of its edges) when it got there by one of these steps:</para>
    /// <list type="bullet">
    /// <item><b>Water to land:</b> orthogonally onto a tile with a Large river along a side parallel to the step. That is a river mouth.</item>
    /// <item><b>Land to land:</b> orthogonally along a continuous Large river.</item>
    /// <item><b>Land to water:</b> orthogonally, always. A ship only reaches land by river, so it can always put back out to sea and is never stranded.</item>
    /// </list>
    /// <para>No diagonal step may involve land, and crossing a river never refuses a ship.</para>
    /// </remarks>
    /// <returns><see langword="null"/> when the step is permitted; otherwise the player-facing reason it is refused.</returns>
    internal static string? GetSailingRefusal(MovementStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (step.Target.IsWater)
        {
            // Open water to open water is the ordinary case. Putting back out from a river is allowed only straight on, not round a corner.
            return step.OriginIsWater || !step.IsDiagonal ? null : "Cannot sail diagonally off a river.";
        }

        if (step.OriginIsWater)
        {
            return !step.IsDiagonal && step.Rivers.TargetBank == RiverWidth.Large ? null : "Cannot enter land.";
        }

        return !step.IsDiagonal && step.Rivers.Along == RiverWidth.Large ? null : "Can only sail along a large river.";
    }

    /// <summary>
    /// What a Water or BelowWater step costs: the tile's cost on water, and a flat <see cref="RIVER_SAILING_COST"/> on a river.
    /// </summary>
    /// <remarks>The land beside a river says nothing about how hard it is to sail up it, so its terrain cost is ignored.</remarks>
    internal static decimal GetSailingStepCost(MovementStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step.Target.IsWater ? step.Target.MovementCost : RIVER_SAILING_COST;
    }

    /// <summary>
    /// The river the diagonal step's corner lies on, if it separates the two tiles.
    /// </summary>
    /// <remarks>
    /// Going round the corner by way of the tile beside the origin (B) crosses the edges A|B and B|D; going by way of the tile above or below it (C)
    /// crosses A|C and C|D. Each route crosses the wider river on it, and the Force takes the easier route.
    /// </remarks>
    private static RiverWidth EvaluateDiagonalCrossing(RiverEdges edges, int deltaX, int deltaY)
    {
        RiverWidth viaHorizontalNeighbour = Wider(edges.GetEdge(0, 0, deltaX, 0), edges.GetEdge(deltaX, 0, deltaX, deltaY));
        RiverWidth viaVerticalNeighbour = Wider(edges.GetEdge(0, 0, 0, deltaY), edges.GetEdge(0, deltaY, deltaX, deltaY));

        return Narrower(viaHorizontalNeighbour, viaVerticalNeighbour);
    }

    /// <summary>The wider of two rivers. <see cref="RiverWidth"/> is ordered by width, so this is a plain comparison.</summary>
    private static RiverWidth Wider(RiverWidth first, RiverWidth second)
    {
        return first >= second ? first : second;
    }

    /// <summary>The narrower of two rivers, which is <see cref="RiverWidth.None"/> when either is.</summary>
    private static RiverWidth Narrower(RiverWidth first, RiverWidth second)
    {
        return first <= second ? first : second;
    }
}
