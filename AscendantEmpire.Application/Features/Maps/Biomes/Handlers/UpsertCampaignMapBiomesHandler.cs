using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Validators;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Biomes;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Handlers;

public record UpsertCampaignMapBiomesRequest(IReadOnlyCollection<AddOrUpdateCampaignMapBiomeDto> Biomes) : IRequest<ServiceResultBase>;

internal class UpsertCampaignMapBiomesHandler(
    ILogger<UpsertCampaignMapBiomesHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator
    ) : IRequestHandler<UpsertCampaignMapBiomesRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(UpsertCampaignMapBiomesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate the request objects
            ServiceResultBase validation = ValidateRequestBiomes(context, request.Biomes);

            if (validation.HasSucceeded == false)
            {
                logger.LogWarning("Validation failed for " + nameof(UpsertCampaignMapBiomesRequest) + ": {Errors}", string.Join(Environment.NewLine, validation.Errors));
                return ServiceResult.Fail(validation.Errors);
            }

            CampaignBiome[] existingBiomes = [.. context.CampaignBiomes.Where(b => request.Biomes.Select(rb => rb.BiomeType).Contains((BiomeType)b.BiomeTypeId))];

            int[] existingBiomeTypeIds = [.. existingBiomes.Select(b => (int)b.BiomeTypeId)];

            // Get any Biomes that aren't in the database
            AddOrUpdateCampaignMapBiomeDto[] missingBiomes = [.. request.Biomes
                .Where(b => existingBiomeTypeIds.Contains((int)b.BiomeType) == false)
                .Select(b => new AddOrUpdateCampaignMapBiomeDto
                {
                    BiomeType = b.BiomeType,
                    Name = b.Name,
                    MovementCost = b.MovementCost,
                    DoesBlockSight = b.DoesBlockSight,
                    IsWater = b.IsWater,
                    IsTraversable = b.IsTraversable,
                    RegenerationPercent = b.RegenerationPercent,
                    BaseForageValue = b.BaseForageValue,
                    BaseHuntValue = b.BaseHuntValue,
                    BaseFishValue = b.BaseFishValue,
                    BaseFodderValue = b.BaseFodderValue,
                    BaseBoneValue = b.BaseBoneValue,
                    BaseWoodValue = b.BaseWoodValue,
                    BaseStoneValue = b.BaseStoneValue,
                    BaseSpecialValue = b.BaseSpecialValue,
                    MapEditorHtmlColor = b.MapEditorHtmlColor,
                    CanBeElevated = b.CanBeElevated,
                    IsPaintableOnCampaignMap = b.IsPaintableOnCampaignMap
                })];

            if (missingBiomes.Length != 0)
            {
                ServiceResultBase insertResult = await mediator.Send(new AddCampaignMapBiomesRequest(missingBiomes));
                if (insertResult.HasSucceeded == false)
                {
                    logger.LogWarning("Failed to insert missing biomes during " + nameof(UpsertCampaignMapBiomesRequest) + ": {Errors}", string.Join(Environment.NewLine, insertResult.Errors));
                    return insertResult;
                }
            }

            Dictionary<BiomeType, AddOrUpdateCampaignMapBiomeDto> requestedUpdates = request.Biomes.ToDictionary(b => b.BiomeType);

            foreach (CampaignBiome existingBiome in existingBiomes)
            {
                AddOrUpdateCampaignMapBiomeDto updatedValues = requestedUpdates[(BiomeType)existingBiome.BiomeTypeId];

                existingBiome.Name = updatedValues.Name;
                existingBiome.MovementCost = updatedValues.MovementCost;
                existingBiome.DoesBlockSight = updatedValues.DoesBlockSight;
                existingBiome.IsWater = updatedValues.IsWater;
                existingBiome.IsTraversable = updatedValues.IsTraversable;
                existingBiome.RegenerationPercent = updatedValues.RegenerationPercent;
                // The DTO carries whole numbers from the UI; store them scaled so bonus math keeps precision.
                existingBiome.BaseForageValue = ScaledResourceValue.ToRaw(updatedValues.BaseForageValue);
                existingBiome.BaseHuntValue = ScaledResourceValue.ToRaw(updatedValues.BaseHuntValue);
                existingBiome.BaseFishValue = ScaledResourceValue.ToRaw(updatedValues.BaseFishValue);
                existingBiome.BaseFodderValue = ScaledResourceValue.ToRaw(updatedValues.BaseFodderValue);
                existingBiome.BaseBoneValue = ScaledResourceValue.ToRaw(updatedValues.BaseBoneValue);
                existingBiome.BaseWoodValue = ScaledResourceValue.ToRaw(updatedValues.BaseWoodValue);
                existingBiome.BaseStoneValue = ScaledResourceValue.ToRaw(updatedValues.BaseStoneValue);
                existingBiome.BaseSpecialValue = ScaledResourceValue.ToRaw(updatedValues.BaseSpecialValue);
                existingBiome.MapEditorHtmlColor = updatedValues.MapEditorHtmlColor;
                existingBiome.CanBeElevated = updatedValues.CanBeElevated;
                existingBiome.IsPaintableOnCampaignMap = updatedValues.IsPaintableOnCampaignMap;
            }

            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            const string error = "Error updating Campaign Map Biomes.";
            logger.LogError(ex, error);
            return ServiceResultBase.InternalServerError(error);
        }

        return ServiceResultBase.SuccessNoContent();
    }

    /// <summary>
    /// Validates the request Biomes against the CampaignMapBiomeValidator.
    /// </summary>
    private static ServiceResultBase ValidateRequestBiomes(AscendantEmpireContext context, IReadOnlyCollection<IAddOrUpdateCampaignMapBiomeDto> biomes)
    {
        var validator = new CampaignMapBiomeValidator();

        // Validate individual Biomes
        (BiomeType BiomeType, ValidationResult ValidationResults)[] validationResults = [.. biomes.Select(biome => (biome.BiomeType, validator.Validate(biome)))];

        Dictionary<BiomeType, string> biomeLookup = biomes.ToDictionary(b => b.BiomeType, b => b.Name);

        string[] groupedErrors = [.. validationResults
            .Where(result => result.ValidationResults.Errors.Count != 0)
            .SelectMany(result =>
                result.ValidationResults.Errors.Select(error =>
                    $"Biome '{biomeLookup[result.BiomeType]}': {error.ErrorMessage}"))];

        // Check for any duplicate BiomeTypeIds in the request object
        string[] duplicateBiomesInRequest = [.. biomes
            .GroupBy(b => b.BiomeType)
            .Where(g => g.Count() > 1)
            .Select(g => $"{biomeLookup[g.Key]} is specified multiple times in the request.")];

        if (groupedErrors.Length != 0 || duplicateBiomesInRequest.Length != 0)
        {
            return ServiceResultBase.Fail(groupedErrors.Concat(duplicateBiomesInRequest));
        }

        return ServiceResultBase.Succeed();
    }
}
