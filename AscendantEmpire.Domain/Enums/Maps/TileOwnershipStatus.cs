namespace AscendantEmpire.Domain.Enums.Maps;

public enum TileOwnershipStatus
{
    Unknown,
    Unclaimed,

    /// <summary>
    /// Claimed by adjacency, influence, explorer action, or decree.
    /// </summary>
    /// <remarks>Will become Owned once the claim is solidified by building, garrison, population loyalty, or time held.</remarks>
    Claimed,

    /// <summary>
    /// Represents a tile that is currently owned by a faction, with full control and rights. Owned by building, garrison, population loyalty, or time held.
    /// </summary>
    /// <remarks>Will become Disputed or Unclaimed if the owner abandons it or is defeated, or if a rival claim is successfully pressed.</remarks>
    Owned,

    /// <summary>
    /// Represents a tile that is currently occupied by enemy forces, temporarily under their control.
    /// </summary>
    /// <remarks>Will become owned again when the attackers/defenders are defeated.</remarks>
    Occupied,

    /// <summary>
    /// Represents a tile that is currently contested between multiple factions, with no clear ownership.
    /// </summary>
    /// <remarks>Will become owned once only one faction remains with control.</remarks>
    Disputed,

    /// <summary>
    /// Represents a tile that was previously owned but has been lost due to abandonment or destruction (without a claim being pressed). 
    /// </summary>
    /// <remarks>It may be in a state of transition, potentially becoming unclaimed again if not reoccupied or reclaimed.</remarks>
    Abandoned
}
