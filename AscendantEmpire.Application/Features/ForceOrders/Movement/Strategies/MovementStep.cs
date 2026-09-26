namespace AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;

/// <summary>
/// Everything the movement rules need to judge and price one step: the tile being entered, where the step comes from, and the rivers around it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MovementTargetTile"/> answers questions about a tile. This answers questions about a step, which depend on more than the tile: whether it
/// crosses or follows a river, and for a ship, whether it starts on water. Like the tile snapshot, it is built once by the handler so a strategy never has to
/// query anything.
/// </para>
/// </remarks>
internal sealed record MovementStep
{
    /// <summary>The tile being stepped onto.</summary>
    public required MovementTargetTile Target { get; init; }

    /// <summary>Whether the tile the Force is leaving is water, by the same rule as <see cref="MovementTargetTile.IsWater"/>.</summary>
    public required bool OriginIsWater { get; init; }

    /// <summary>Whether the step is one of the four diagonals.</summary>
    public required bool IsDiagonal { get; init; }

    /// <summary>The rivers the step crosses and runs alongside.</summary>
    public required RiverStepEffect Rivers { get; init; }
}
