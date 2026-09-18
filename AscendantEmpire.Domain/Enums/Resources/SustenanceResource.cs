namespace AscendantEmpire.Domain.Enums.Resources;

/// <summary>
/// The four renewable food resources a campaign tile's biome provides, and the only biome values a Player can raise or lower for themselves.
/// </summary>
/// <remarks>
/// The material values (Bone, Wood, Stone, Special) are deliberately absent: they are static, the same for every Player in every game, so nothing ever modifies them.
/// See the Biome section of <c>docs/design/campaign-map/tiles.md</c>.
/// </remarks>
public enum SustenanceResource
{
    Forage = 1,
    Hunt = 2,
    Fish = 3,
    Fodder = 4
}
