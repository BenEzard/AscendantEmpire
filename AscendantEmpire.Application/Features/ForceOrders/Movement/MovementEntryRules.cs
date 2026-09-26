using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement;

/// <summary>
/// The single answer to "may a Force of this movement domain enter this tile?", in the order the rules apply.
/// </summary>
/// <remarks>
/// <para>
/// Two callers ask it. <c>StepForceHandler</c> asks because a player is trying to move, and the fog-of-war read asks because an Observer blinded by
/// dense terrain reveals precisely the adjacent tiles it could never have walked onto to find out what was there. Those must agree: the map would
/// otherwise show a player water it then refused to let them step into, or refuse a step onto ground it had never revealed.
/// </para>
/// <para>
/// This is deliberately the opposite call to the one <see cref="ContextActions.Strategies.SettlementFoundingRules"/> makes. That class refuses to
/// share <see cref="MovementTargetTile"/> because settling is a <i>different</i> question asked of the same tile, so a later change to the movement
/// rules must not quietly move it. Entering is not a different question — it is this one — so here sharing is the point.
/// </para>
/// </remarks>
internal static class MovementEntryRules
{
    /// <summary>
    /// Judges whether a Force of the given domain may enter the tile.
    /// </summary>
    /// <param name="movementDomainStrategies">Every registered domain strategy, as injected.</param>
    /// <param name="movementDomain">The domain doing the entering.</param>
    /// <param name="target">The tile being entered.</param>
    /// <returns><see langword="null"/> when entry is permitted; otherwise why it is refused.</returns>
    /// <remarks>
    /// Impassability is settled before any strategy is consulted, because it holds for every domain alike — nothing enters a mountain or the Void,
    /// however it moves.
    /// </remarks>
    internal static MovementEntryRefusal? GetEntryRefusal(
        IEnumerable<IMovementDomainStrategy> movementDomainStrategies,
        DomainType movementDomain,
        MovementTargetTile target)
    {
        ArgumentNullException.ThrowIfNull(movementDomainStrategies);
        ArgumentNullException.ThrowIfNull(target);

        MovementEntryRefusal? impassable = GetImpassableRefusal(target);

        if (impassable is not null)
        {
            return impassable;
        }

        IMovementDomainStrategy? strategy = FindStrategy(movementDomainStrategies, movementDomain);

        if (strategy is null)
        {
            return MissingStrategyRefusal(movementDomain);
        }

        string? refusal = strategy.GetEntryRefusal(target);

        return refusal is null ? null : new MovementEntryRefusal(refusal, IsRuleFault: false);
    }

    /// <summary>
    /// The refusal for a tile nothing can enter, whatever its domain; <see langword="null"/> when the tile is passable.
    /// </summary>
    /// <remarks>Shared with <see cref="MovementStepRules"/>, so a step and a tile question refuse an impassable tile in the same words.</remarks>
    internal static MovementEntryRefusal? GetImpassableRefusal(MovementTargetTile target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!target.IsImpassable)
        {
            return null;
        }

        // The two causes read differently to a player, so a mountain is named as one; anything else keeps the general wording.
        string reason = target.GeographyIsImpassable ? "Mountain terrain cannot be entered." : "This terrain cannot be entered.";
        return new MovementEntryRefusal(reason, IsRuleFault: false);
    }

    /// <summary>The strategy registered for a domain, or <see langword="null"/> when none is, which is a wiring fault.</summary>
    internal static IMovementDomainStrategy? FindStrategy(IEnumerable<IMovementDomainStrategy> movementDomainStrategies, DomainType movementDomain)
    {
        ArgumentNullException.ThrowIfNull(movementDomainStrategies);

        return movementDomainStrategies.SingleOrDefault(s => s.MovementDomain == movementDomain);
    }

    /// <summary>The refusal given when no strategy is registered for a domain.</summary>
    /// <remarks>
    /// Every DomainType has a registered strategy, so this is a wiring fault rather than anything the player did. Refusing entry is the safe reading of it:
    /// better a Force that cannot move than one that walks anywhere at all.
    /// </remarks>
    internal static MovementEntryRefusal MissingStrategyRefusal(DomainType movementDomain)
    {
        return new MovementEntryRefusal($"No movement rules are registered for the {movementDomain} domain.", IsRuleFault: true);
    }
}

/// <summary>
/// Why a Force was refused entry to a tile.
/// </summary>
/// <param name="Reason">The player-facing explanation, phrased so it can be shown verbatim.</param>
/// <param name="IsRuleFault">
/// Whether the refusal is a fault in the rules themselves rather than a decision about the terrain. Callers log those two very differently: one is
/// an ordinary move a player cannot make, the other is a bug.
/// </param>
internal sealed record MovementEntryRefusal(string Reason, bool IsRuleFault);
