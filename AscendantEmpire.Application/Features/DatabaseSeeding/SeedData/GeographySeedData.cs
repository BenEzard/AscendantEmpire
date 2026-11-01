using AscendantEmpire.BackendContracts.Geographies.AddCampaignMapGeography;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>map.CampaignGeographies</c> values applied by the database seed.
/// </summary>
/// <remarks>
/// Transcribed from <c>Assets/Documentation/Installation scripts/CampaignMapGeographyType.json</c>.
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
            // Unknown is not paintable and stands for "no data", so it is not traversable: a data gap must not read as open ground.
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.Unknown,
                Name = "Unknown",
                RelativeElevation = 0,
                MovementCost = 1m,
                IsWater = false,
                IsTraversable = false,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = false,
            },
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.DeepWater,
                Name = "Deep Water",
                RelativeElevation = -2,
                MovementCost = 1m,
                IsWater = true,
                IsTraversable = true,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = true,
            },
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.ShallowWater,
                Name = "Shallow Water",
                RelativeElevation = -1,
                MovementCost = 1m,
                IsWater = true,
                IsTraversable = true,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = true,
            },
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.Flat,
                Name = "Flat",
                RelativeElevation = 0,
                MovementCost = 1m,
                IsWater = false,
                IsTraversable = true,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = true,
            },
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.Hilly,
                Name = "Hilly",
                RelativeElevation = 1,
                MovementCost = 2m,
                IsWater = false,
                IsTraversable = true,
                MapEditorIcon = "Hill",
                IsPaintableOnCampaignMap = true,
            },
            new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = CampaignMapGeographyType.Mountainous,
                Name = "Mountainous",
                RelativeElevation = 2,
                MovementCost = CampaignGeography.IMPASSABLE_MOVEMENT_COST,
                IsWater = false,
                IsTraversable = false,
                MapEditorIcon = "Mountain",
                IsPaintableOnCampaignMap = true,
            },
        ];
    }
}
