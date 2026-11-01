using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Geographies;

namespace AscendantEmpire.BackendContracts.Geographies.AddCampaignMapGeography;
public record AddOrUpdateCampaignMapGeographyDto : IAddOrUpdateCampaignMapGeographyDto
{
    /// <inheritdoc />
    public required CampaignMapGeographyType CampaignMapGeographyType { get; set; }

    /// <inheritdoc />
    public required int RelativeElevation { get; set; }

    /// <inheritdoc />
    public required string Name { get; set; }

    /// <inheritdoc />
    public required decimal MovementCost { get; set; }

    /// <inheritdoc />
    public required bool IsWater { get; set; }

    /// <inheritdoc />
    public required bool IsTraversable { get; set; }

    /// <inheritdoc />
    public required string? MapEditorIcon { get; set; }

    /// <inheritdoc />
    public required bool IsPaintableOnCampaignMap { get; set; }
}
