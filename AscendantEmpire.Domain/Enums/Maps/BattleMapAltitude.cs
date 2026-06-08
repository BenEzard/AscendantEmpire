namespace AscendantEmpire.Domain.Enums.Maps;

/// <summary>
/// Altitude levels for individual BattleMap tiles.
///
/// The values are intentionally signed and sequential so arithmetic comparisons work
/// (e.g. diff = neighbourAltitude - thisAltitude gives the slope).
///
///   BelowWater (-1) — submerged, always assigned a water biome.
///   Ground      (0) — sea level / base land.
///   Low         (1) — gentle rise.
///   Medium      (2) — significant hill.
///   High        (3) — steep terrain.
///   Peak        (4) — cliff-top / mountain summit within the battle area.
///
/// Adjacent tiles may differ by more than 1 level, representing cliff faces.
/// Unit movement cost is driven by the altitude difference, not the absolute value.
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
