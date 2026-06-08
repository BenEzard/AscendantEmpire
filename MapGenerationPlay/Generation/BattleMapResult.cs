namespace MapGenerationPlay.Generation;

/// <summary>
/// Output of BattleMapGenerator: altitude and biome per tile, plus the local seed used.
/// </summary>
public record BattleMapResult(
    BattleMapAltitude[] Altitudes,
    BiomeType[]         Biomes,
    long                LocalSeed
);
