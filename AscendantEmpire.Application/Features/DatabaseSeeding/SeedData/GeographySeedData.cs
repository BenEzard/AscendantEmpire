using AscendantEmpire.BackendContracts.Geographies.AddCampaignMapGeography;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>map.CampaignGeographies</c> values applied by the database seed.
/// </summary>
/// <remarks>
/// Transcribed from <c>Resources/Documentation/Installation scripts/CampaignMapGeographyType.json</c>. The
/// <see cref="CampaignMapGeographyType"/> enum has an intentional gap at value 5, so Mountainous is 6.
/// </remarks>
internal static class GeographySeedData
{
    /// <summary>
    /// Builds the campaign map geography rows to seed.
    /// </summary>
    public static IReadOnlyCollection<AddOrUpdateCampaignMapGeographyDto> Build()
    {
        return
        [
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.Unknown,
                Name = "Unknown",
                RelativeElevation = 0,
                MovementCost = 1m,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = false,
            },
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.DeepWater,
                Name = "Deep Water",
                RelativeElevation = -2,
                MovementCost = 1m,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = true,
            },
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.ShallowWater,
                Name = "Shallow Water",
                RelativeElevation = -1,
                MovementCost = 1m,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = true,
            },
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.Flat,
                Name = "Flat",
                RelativeElevation = 0,
                MovementCost = 1m,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = true,
            },
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.Hilly,
                Name = "Hilly",
                RelativeElevation = 1,
                MovementCost = 2m,
                MapEditorIcon = "Hill",
                IsPaintableOnCampaignMap = true,
            },
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.Mountainous,
                Name = "Mountainous",
                RelativeElevation = 2,
                MovementCost = 999m,
                MapEditorIcon = "Mountain",
                IsPaintableOnCampaignMap = true,
            },
        ];
    }
}
