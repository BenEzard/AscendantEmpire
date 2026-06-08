namespace MapGenerationPlay.Generation;

/// <summary>
/// Campaign-map biome types, mirroring BiomeType in the domain.
/// Kept local so MapGenerationPlay has no project dependency on the domain.
/// </summary>
public enum BiomeType
{
    Coastal   = 1,
    Ocean     = 2,
    Desert    = 3,
    Arid      = 4,
    Ice       = 5,
    Tundra    = 6,
    Grassland = 7,
    Forest    = 8,
    Jungle    = 9,
    Woodland  = 10,
}
