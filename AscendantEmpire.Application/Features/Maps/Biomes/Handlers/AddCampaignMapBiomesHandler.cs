using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Validators;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Handlers;

public record AddCampaignMapBiomesRequest(IReadOnlyCollection<AddOrUpdateCampaignMapBiomeDto> Biomes) : IRequest<ServiceResultBase>;

internal class AddCampaignMapBiomesHandler(
    ILogger<AddCampaignMapBiomesHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<AddCampaignMapBiomesRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(AddCampaignMapBiomesRequest request, CancellationToken cancellationToken)
    {
        // Validate the request objects
        ServiceResultBase validation = CampaignMapBiomeValidator.ValidateBiomes(request.Biomes);

        if (validation.HasSucceeded == false)
        {
            logger.LogWarning("Validation failed for " + nameof(AddCampaignMapBiomesRequest) + ": {Errors}", string.Join(Environment.NewLine, validation.Errors));
            return ServiceResult.Fail(validation.Errors);
        }

        // BiomeTypeId is the key, so a Biome that is already stored cannot be added a second time. This is checked here rather than in
        // CampaignMapBiomeValidator because UpsertCampaignMapBiomesHandler validates a request that deliberately names Biomes that already exist, and then
        // updates them — the rule belongs to adding, not to the shape of the request.
        BiomeType[] requestedTypes = [.. request.Biomes.Select(biome => biome.BiomeType)];

        BiomeType[] alreadyStored = await context.CampaignBiomes
            .Where(biome => requestedTypes.Contains(biome.BiomeTypeId))
            .Select(biome => biome.BiomeTypeId)
            .ToArrayAsync(cancellationToken);

        if (alreadyStored.Length != 0)
        {
            string[] errors = [.. alreadyStored.Select(type => $"{type} is already a Biome. Either delete the existing Biome or edit it.")];

            logger.LogWarning(
                "Refused " + nameof(AddCampaignMapBiomesRequest) + " for {BiomeCount} Biomes already stored: {Biomes}.",
                alreadyStored.Length,
                string.Join(", ", alreadyStored));

            return ServiceResultBase.Fail(errors);
        }

        // Map the request DTOs to domain entities
        var campaignBiomes = request.Biomes.Select(biome => new CampaignBiome
        {
            BiomeTypeId = biome.BiomeType,
            Name = biome.Name,
            MovementCost = biome.MovementCost,
            DoesBlockSight = biome.DoesBlockSight,
            IsWater = biome.IsWater,
            IsTraversable = biome.IsTraversable,
            RegenerationPercent = biome.RegenerationPercent,
            // The DTO carries whole numbers from the UI; store them scaled so bonus math keeps precision.
            BaseForageValue = ScaledResourceValue.ToRaw(biome.BaseForageValue),
            BaseHuntValue = ScaledResourceValue.ToRaw(biome.BaseHuntValue),
            BaseFishValue = ScaledResourceValue.ToRaw(biome.BaseFishValue),
            BaseFodderValue = ScaledResourceValue.ToRaw(biome.BaseFodderValue),
            BaseBoneValue = ScaledResourceValue.ToRaw(biome.BaseBoneValue),
            BaseWoodValue = ScaledResourceValue.ToRaw(biome.BaseWoodValue),
            BaseStoneValue = ScaledResourceValue.ToRaw(biome.BaseStoneValue),
            MapEditorHtmlColor = biome.MapEditorHtmlColor,
            CanBeElevated = biome.CanBeElevated,
            IsPaintableOnCampaignMap = biome.IsPaintableOnCampaignMap
        }).ToArray();

        context.CampaignBiomes.AddRange(campaignBiomes);

        await context.SaveChangesAsync(CancellationToken.None);

        return ServiceResultBase.Created();
    }
}
