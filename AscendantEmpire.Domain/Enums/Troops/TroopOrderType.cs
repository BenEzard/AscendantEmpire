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
    Move = 1,

    /// <summary>
    /// Troop is building a structure on a specific tile.
    /// </summary>
    Build = 2,

    /// <summary>
    /// Troop is healing/repairing on a specific tile.
    /// </summary>
    Heal = 3,

    /// <summary>
    /// Troop is setting up an ambush on a specific tile, waiting for an enemy to enter.
    /// </summary>
    Ambush = 4,

    /// <summary>
    /// Troop is building fortifications on a specific tile, improving its defensive capabilities.
    /// </summary>
    Fortify = 5,

    /// <summary>
    /// Troop is awaiting vision of an enemy unit, and will then "wake".
    /// </summary>
    Sentry = 6,

    /// <summary>
    /// Troop is gathering resources on a specific tile.
    /// </summary>
    Gather = 7,

    /// <summary>
    /// Skip turn.
    /// </summary>
    SkipTurn = 8,

    /// <summary>
    /// Troop is auto-exploring.
    /// </summary>
    AutoExplore = 9,

    /// <summary>
    /// Troops are spending movement points to combine forces.
    /// </summary>
    CombineTroops = 10,

    /// <summary>
    /// Troops are spending movement points to split into separate forces.
    /// </summary>
    SplitTroops = 11,

    /// <summary>
    /// Troop is conducting a trade mission.
    /// </summary>
    Trade = 12,

    /// <summary>
    /// Troop is taking part in a supply operation.
    /// </summary>
    Supply = 13,

    /// <summary>
    /// Troop is defending a specific target (troop or structure) against an attack.
    /// </summary>
    Defend = 14,


    Pillage = 15,
}
