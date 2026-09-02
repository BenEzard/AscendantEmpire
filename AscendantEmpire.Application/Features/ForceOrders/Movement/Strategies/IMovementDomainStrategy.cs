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
}
