using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Geographies.GetCampaignMapGeographies;

namespace AscendantEmpire.Application.Handlers.Maps.Geographies.Classes;
public class DTCampaignMapGeography : ICampaignMapGeography
{
    public CampaignMapGeographyType CampaignMapGeographyType { get; set; }

    public required string Name { get; set; }

    public required int RelativeElevation { get; set; }

    public required decimal MovementCost { get; set; }

    public required string? MapEditorIcon { get; set; }

    public required bool IsPaintableOnCampaignMap { get; set; }
}
