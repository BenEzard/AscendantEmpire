namespace MapGenerationPlay.Generation;

/// <summary>
/// Campaign-map geography types, mirroring CampaignMapGeographyType in the domain.
/// Kept as a local enum here so MapGenerationPlay has no dependency on the domain project.
/// </summary>
public enum GeographyType
{
    DeepWater    = 1,
    ShallowWater = 2,
    Flat         = 3,
    Hilly        = 4,
    Mountainous  = 6,   // intentional gap — matches domain enum values
}
