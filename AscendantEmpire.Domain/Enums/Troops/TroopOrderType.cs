namespace AscendantEmpire.Domain.Enums.Troops;

public enum TroopOrderType
{
    None = 0,

    /// <summary>
    /// Troop is moving to a specific destination tile. 
    /// </summary>
    /// <remarks>TargetCampaignMapTileId is the destination tile. 
    /// For movement orders, CurrentMovementStepIndex tracks progress along the path to the destination.
    /// </remarks>
    Move = 100,

    /// <summary>
    /// Troop is building a Structure on a specific tile.
    /// </summary>
    Build = 200,

    /// <summary>
    /// Troop is healing/repairing on a specific tile.
    /// </summary>
    Heal = 300,

    /// <summary>
    /// Troop is setting up an ambush on a specific tile, waiting for an enemy to enter.
    /// </summary>
    Ambush = 400,

    /// <summary>
    /// Troop is building fortifications on a specific tile, improving its defensive capabilities.
    /// </summary>
    Fortify = 500,

    /// <summary>
    /// Troop is awaiting vision of an enemy unit, and will then "wake".
    /// </summary>
    Sentry = 600,

    /// <summary>
    /// Troop is gathering resources on a specific tile.
    /// </summary>
    Gather = 700,

    /// <summary>
    /// Skip turn.
    /// </summary>
    SkipTurn = 800,

    /// <summary>
    /// Troop is auto-exploring.
    /// </summary>
    AutoExplore = 900,

    /// <summary>
    /// Troops are spending movement points to combine forces.
    /// </summary>
    CombineTroops = 1000,

    /// <summary>
    /// Troops are spending movement points to split into separate forces.
    /// </summary>
    SplitTroops = 1100,

    /// <summary>
    /// Troop is conducting a trade mission.
    /// </summary>
    Trade = 1200,

    /// <summary>
    /// Troop is taking part in a supply operation.
    /// </summary>
    Supply = 1300,

    /// <summary>
    /// Troop is defending a specific target (troop or structure) against an attack.
    /// </summary>
    Defend = 1400,


    Pillage = 1500,
}
