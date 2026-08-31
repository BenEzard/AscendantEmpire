namespace AscendantEmpire.Domain.Enums.Forces;

public enum ForceOrderType
{
    None = 0,

    /// <summary>
    /// Force is moving to a specific destination tile. 
    /// </summary>
    /// <remarks>TargetCampaignMapTileId is the destination tile. 
    /// For movement orders, CurrentMovementStepIndex tracks progress along the path to the destination.
    /// </remarks>
    Move = 100,

    /// <summary>
    /// Force is building a Structure on a specific tile.
    /// </summary>
    Build = 200,

    /// <summary>
    /// Force is healing/repairing on a specific tile.
    /// </summary>
    Heal = 300,

    /// <summary>
    /// Force is setting up an ambush on a specific tile, waiting for an enemy to enter.
    /// </summary>
    Ambush = 400,

    /// <summary>
    /// Force is building fortifications on a specific tile, improving its defensive capabilities.
    /// </summary>
    Fortify = 500,

    /// <summary>
    /// Force is awaiting vision of an enemy unit, and will then "wake".
    /// </summary>
    Sentry = 600,

    /// <summary>
    /// Force is gathering resources on a specific tile.
    /// </summary>
    Gather = 700,

    /// <summary>
    /// Skip turn.
    /// </summary>
    SkipTurn = 800,

    /// <summary>
    /// Force is auto-exploring.
    /// </summary>
    AutoExplore = 900,

    /// <summary>
    /// Forces are spending movement points to combine Forces.
    /// </summary>
    CombineForces = 1000,

    /// <summary>
    /// Forces are spending movement points to split into separate Forces.
    /// </summary>
    SplitForce = 1100,

    /// <summary>
    /// Force is conducting a trade mission.
    /// </summary>
    Trade = 1200,

    /// <summary>
    /// Force is taking part in a supply operation.
    /// </summary>
    Supply = 1300,

    /// <summary>
    /// Force is defending a specific target (Force or structure) against an attack.
    /// </summary>
    Defend = 1400,


    Pillage = 1500,
}
