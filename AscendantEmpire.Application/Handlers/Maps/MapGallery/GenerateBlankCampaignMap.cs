using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using System.Text.Json;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery;

internal class GenerateBlankCampaignMap(
    IServiceScopeFactory scopeFactory
) : ICampaignMapGenerator
{
    private BiomeType _defaultBiomeType;
    private CampaignMapGeographyType _defaultGeographyType;

    public async Task<ServiceResult<int>> GenerateCampaignMapAsync(Domain.Entities.Maps.CampaignMaps.CampaignMapGalleryIndex index)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        using AscendantEmpireContext context = scope.ServiceProvider.GetRequiredService<AscendantEmpireContext>();

        var sql = """
BEGIN TRAN;

DECLARE @StartId int =
    ISNULL(
        (SELECT MAX(CampaignMapGalleryId)
         FROM [mapgallery].[CampaignMapGallery] WITH (HOLDLOCK, TABLOCKX)),
        0
    ) + 1;

;WITH dims AS (
    SELECT Id, Width, Height
    FROM [mapgallery].[CampaignMapIndex]
    WHERE Id = @IndexId
),
x AS (SELECT n.Id AS X FROM util.Numbers n JOIN dims d ON n.Id < d.Width),
y AS (SELECT n.Id AS Y FROM util.Numbers n JOIN dims d ON n.Id < d.Height),
missing AS (
    SELECT d.Id AS CampaignMapIndexId, x.X AS XCoord, y.Y AS YCoord
    FROM dims d
    CROSS JOIN x
    CROSS JOIN y
),
to_insert AS (
    SELECT
        CampaignMapIndexId, XCoord, YCoord,
        rn = ROW_NUMBER() OVER (ORDER BY YCoord, XCoord)
    FROM missing
)
INSERT INTO [mapgallery].[CampaignMapGallery] (
    CampaignMapGalleryId,
    CampaignMapGalleryIndexId,
    XCoord, 
    YCoord,
    BiomeTypeId,
    CampaignMapGeographyTypeId
)
SELECT
    @StartId + (rn - 1),
    CampaignMapIndexId,
    XCoord, YCoord,
    @BiomeTypeId AS BiomeTypeId,
    @GeographyTypeId AS GeographyTypeId
FROM to_insert;

COMMIT TRAN;
""";

        var indexIdParam = new SqlParameter("@IndexId", index.Id);
        var biomeTypeParam = new SqlParameter("@BiomeTypeId", (int)_defaultBiomeType);
        var geographyTypeParam = new SqlParameter("@GeographyTypeId", (int)_defaultGeographyType);

        await context.Database.ExecuteSqlRawAsync(sql, indexIdParam, biomeTypeParam, geographyTypeParam);

        int startId = await context.CampaignMapGallery
            .Where(cmg => cmg.CampaignMapGalleryIndexId == index.Id)
            .OrderBy(cmg => cmg.CampaignMapGalleryId)
            .Select(cmg => cmg.CampaignMapGalleryId)
            .FirstAsync();

        return ServiceResult<int>.Succeed(startId);
    }

    public ServiceResultBase ValidateRequestProperties(Domain.Entities.Maps.CampaignMaps.CampaignMapGalleryIndex index)
    {
        // Get the dictionary
        Dictionary<string, string>? propertyDictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(index.PropertiesJson);
    
        if (propertyDictionary is not null)
        {
            _defaultBiomeType = propertyDictionary.TryGetValue("defaultBiome", out var biomeTypeStr) &&
                                     Enum.TryParse<BiomeType>(biomeTypeStr, out var biomeType)
                                         ? biomeType
                                         : BiomeType.Ocean;

            _defaultGeographyType = CampaignMapGeographyType.Flat;
        }

       

        return ServiceResultBase.Succeed();
    }
}
