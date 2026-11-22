namespace AscendantEmpire.Domain.Enums.Maps;

/// <summary>
/// The material resources a campaign tile's biome provides, which are the same for every Player and are never depleted.
/// </summary>
/// <remarks>
/// The counterpart to <see cref="SustenanceResource"/>, and deliberately a separate enum rather than more members on it: a sustenance value is
/// player-specific, depletes into <c>CampaignMapDepletion</c> and regrows, and a material value does none of the three. The amount a tile states can be
/// extracted every Turn indefinitely. See the Biome section of <c>docs/design/campaign-map/tiles.md</c>.
/// </remarks>
public enum MaterialResource
{
    Wood = 1,
    Stone = 2,
    Bone = 3
}
