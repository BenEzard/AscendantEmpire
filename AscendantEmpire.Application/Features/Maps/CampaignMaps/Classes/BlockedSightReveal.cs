using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// What an Observer blinded by dense terrain can still make out: the adjacent Tiles it could never have walked onto to find out.
/// </summary>
/// <remarks>
/// <para>
/// A sight-blocking Biome zeroes sight — see <see cref="ObserverSightRules"/> — leaving the Tile the Observer occupies as all it can report. That is
/// the mechanic, and it works everywhere except at the boundary of what a Force can reach: an adjacent Tile it is barred from entering is a Tile it
/// can never discover by any means. Water beside a coastal Jungle, and a mountain or the Void beside anything, would stay unknown for the whole
/// game. This rule hands those back, and nothing else.
/// </para>
/// <para>
/// The judgement is deferred wholesale to <see cref="MovementEntryRules"/>, the same authority the step handler asks. Reveal and refusal are then
/// the same question with the same answer, so the map can never show a Player water it would then let them sail into, nor refuse a step onto ground
/// it had never revealed.
/// </para>
/// <para>Pure over its arguments and free of the database, so it is testable without a container. The query that feeds it lives in the handler.</para>
/// </remarks>
internal static class BlockedSightReveal
{
    /// <summary>
    /// Picks out the adjacent Tiles a blinded Observer of this movement domain reveals.
    /// </summary>
    /// <param name="movementDomainStrategies">Every registered domain strategy, as injected.</param>
    /// <param name="movementDomain">The blinded Observer's domain. A Structure has none of its own and is passed <see cref="DomainType.Land"/>.</param>
    /// <param name="adjacentTiles">The Tiles around the Observer, already narrowed to its eight neighbours.</param>
    /// <returns>Those the Observer is barred from entering, which may be none — the usual answer deep inside a Jungle.</returns>
    /// <remarks>
    /// <para>
    /// The rule is symmetric, and deliberately so. A ship sitting in a Mangrove reveals the land around it for the same reason a Force in a Jungle
    /// reveals the sea: neither could have gone there to look.
    /// </para>
    /// <para>
    /// A refusal that is a fault in the rules rather than a judgement about the terrain reveals <b>nothing</b>. The step handler refuses the move in
    /// that case, because a Force that cannot move is safer than one that walks anywhere; vision has to fail the other way, or an unregistered
    /// strategy would quietly hand the Player the whole ring around every blinded Observer they own.
    /// </para>
    /// </remarks>
    internal static IReadOnlyCollection<MovementTargetTile> SelectRevealedTiles(
        IEnumerable<IMovementDomainStrategy> movementDomainStrategies,
        DomainType movementDomain,
        IEnumerable<MovementTargetTile> adjacentTiles)
    {
        ArgumentNullException.ThrowIfNull(adjacentTiles);

        return
        [
            .. adjacentTiles.Where(tile =>
                MovementEntryRules.GetEntryRefusal(movementDomainStrategies, movementDomain, tile) is { IsRuleFault: false })
        ];
    }
}
