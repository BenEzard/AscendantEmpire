namespace MapGenerationPlay.Generation;

/// <summary>
/// Altitude levels for BattleMap tiles. Mirrors BattleMapAltitude in the domain.
/// </summary>
public enum BattleMapAltitude
{
    BelowWater = -1,
    Ground     =  0,
    Low        =  1,
    Medium     =  2,
    High       =  3,
    Peak       =  4,
}
