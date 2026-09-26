using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement;

/// <summary>
/// The single answer to "may a Force of this movement domain take this step?", rivers included.
/// </summary>
/// <remarks>
/// <para>
/// The step-shaped counterpart of <see cref="MovementEntryRules"/>, which answers the tile-shaped question fog of war needs. The two agree about
/// everything except rivers, which belong to a step and not to a tile: a Land Force can be refused a step across a Large river onto a tile it could
/// happily enter from the other side, and a ship can sail up a Large river onto land it could never enter from the sea anywhere else.
/// </para>
/// <para>
/// Impassability is settled first and in the same words as the tile rule, because nothing enters a mountain or the Void however it moves or whatever
/// river it follows.
/// </para>
/// </remarks>
internal static class MovementStepRules
{
    /// <summary>
    /// Judges whether a Force of the given domain may take the step.
    /// </summary>
    /// <param name="movementDomainStrategies">Every registered domain strategy, as injected.</param>
    /// <param name="movementDomain">The domain doing the stepping.</param>
    /// <param name="step">The step being taken.</param>
    /// <returns><see langword="null"/> when the step is permitted; otherwise why it is refused.</returns>
    internal static MovementEntryRefusal? GetStepRefusal(
        IEnumerable<IMovementDomainStrategy> movementDomainStrategies,
        DomainType movementDomain,
        MovementStep step)
    {
        ArgumentNullException.ThrowIfNull(movementDomainStrategies);
        ArgumentNullException.ThrowIfNull(step);

        MovementEntryRefusal? impassable = MovementEntryRules.GetImpassableRefusal(step.Target);

        if (impassable is not null)
        {
            return impassable;
        }

        IMovementDomainStrategy? strategy = MovementEntryRules.FindStrategy(movementDomainStrategies, movementDomain);

        if (strategy is null)
        {
            return MovementEntryRules.MissingStrategyRefusal(movementDomain);
        }

        string? refusal = strategy.GetStepRefusal(step);

        return refusal is null ? null : new MovementEntryRefusal(refusal, IsRuleFault: false);
    }

    /// <summary>
    /// What a Force of the given domain pays for a step <see cref="GetStepRefusal"/> has permitted.
    /// </summary>
    /// <exception cref="InvalidOperationException">No strategy is registered for the domain, which <see cref="GetStepRefusal"/> would have refused.</exception>
    internal static decimal GetStepCost(
        IEnumerable<IMovementDomainStrategy> movementDomainStrategies,
        DomainType movementDomain,
        MovementStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        IMovementDomainStrategy strategy = MovementEntryRules.FindStrategy(movementDomainStrategies, movementDomain)
            ?? throw new InvalidOperationException($"No movement rules are registered for the {movementDomain} domain, so the step cannot be priced.");

        return strategy.GetStepCost(step);
    }
}
