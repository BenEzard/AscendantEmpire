using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;

/// <summary>
/// Decides whether a Force of one movement domain may enter a given tile.
/// </summary>
/// <remarks>
/// <para>
/// One implementation per <see cref="DomainType"/>, selected from the Force's compiled template. Implementations are pure and synchronous:
/// everything they need arrives on the <see cref="MovementTargetTile"/> the handler builds, so a strategy must never query the database. That is
/// the same contract <c>IForceActionStrategy</c> works to.
/// </para>
/// <para>
/// A strategy answers only the terrain question its domain is responsible for. Whether the tile is passable at all, and whether the Force can
/// afford the crossing, are decided by the handler before it ever gets here, because those rules hold for every domain alike.
/// </para>
/// </remarks>
internal interface IMovementDomainStrategy
{
    /// <summary>The movement domain this strategy speaks for.</summary>
    DomainType MovementDomain { get; }

    /// <summary>
    /// Judge whether a Force of this domain may enter the target tile.
    /// </summary>
    /// <param name="target">The tile the Force is trying to step onto.</param>
    /// <returns>
    /// <see langword="null"/> when entry is permitted; otherwise the player-facing reason it is refused, phrased so it can be shown verbatim.
    /// </returns>
    string? GetEntryRefusal(MovementTargetTile target);

    /// <summary>
    /// Judge whether a Force of this domain may take this particular step, rivers included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The step handler asks this; the fog-of-war read asks <see cref="GetEntryRefusal"/>. They differ only where a step's shape matters, which today
    /// means rivers: a Land Force cannot cross a Large river, and a ship can leave the sea to sail up one. Impassable tiles are refused before either
    /// is consulted.
    /// </para>
    /// </remarks>
    /// <param name="step">The step the Force is trying to take.</param>
    /// <returns>
    /// <see langword="null"/> when the step is permitted; otherwise the player-facing reason it is refused, phrased so it can be shown verbatim.
    /// </returns>
    string? GetStepRefusal(MovementStep step);

    /// <summary>
    /// What a Force of this domain pays, in movement points, for a step this strategy has permitted.
    /// </summary>
    /// <remarks>Meaningless for a step <see cref="GetStepRefusal"/> refuses, so ask that first.</remarks>
    decimal GetStepCost(MovementStep step);
}
