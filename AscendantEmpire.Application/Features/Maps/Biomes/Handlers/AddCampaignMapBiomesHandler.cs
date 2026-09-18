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

public record AddCampaignMapBiomesRequest(IReadOnlyCollection<AddOrUpdateCampaignMapBiomeDto> Biomes) : IRequest<ServiceResultBase>;

internal class AddCampaignMapBiomesHandler(
    ILogger<AddCampaignMapBiomesHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<AddCampaignMapBiomesRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(AddCampaignMapBiomesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate the request objects
            ServiceResultBase validation = ValidateRequestBiomes(context, request.Biomes);

            if (validation.HasSucceeded == false)
            {
                logger.LogWarning("Validation failed for " + nameof(AddCampaignMapBiomesRequest) + ": {Errors}", string.Join(Environment.NewLine, validation.Errors));
                return ServiceResult.Fail(validation.Errors);
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
        }
        catch (Exception ex)
        {
            const string error = "Error adding Campaign Map Biomes.";
            logger.LogError(ex, error);
            return ServiceResultBase.InternalServerError(error);
        }

        return ServiceResultBase.Created();
    }

    /// <summary>
    /// Validates the request Biomes against the CampaignMapBiomeValidator.
    /// </summary>
    /// <remarks>Checks there isn't already a record with the same BiomeTypeId in the table</remarks>
    private static ServiceResultBase ValidateRequestBiomes(AscendantEmpireContext context, IReadOnlyCollection<IAddOrUpdateCampaignMapBiomeDto> biomes)
    {
        var validator = new CampaignMapBiomeValidator();

        // Validate individual Biomes
        (BiomeType BiomeType, ValidationResult ValidationResults)[] validationResults = [.. biomes.Select(biome => (biome.BiomeType, validator.Validate(biome)))];

        string[] groupedErrors = [.. validationResults
            .Where(result => result.ValidationResults.Errors.Count != 0)
            .SelectMany(result =>
                result.ValidationResults.Errors.Select(error =>
                    $"Biome '{result.BiomeType}': {error.ErrorMessage}"))];

        // Check the Biome Type Id isn't already in the database
        BiomeType[] biomeTypeIdsInRequest = [.. biomes.Select(b => b.BiomeType)];
        string[] duplicateBiomesInDatabase = [.. context.CampaignBiomes
            .Select(b => b.BiomeTypeId)
            .Where(id => biomeTypeIdsInRequest.Contains(id))
            .Select(id => $"{(BiomeType)id} is already a Biome. Either delete the existing Biome or edit it.")];

        // Check for any duplicate BiomeTypeIds in the request object
        string[] duplicateBiomesInRequest = [.. biomes
            .GroupBy(b => b.BiomeType)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} is specified multiple times in the request.")];

        string[] duplicateErrors = [.. duplicateBiomesInDatabase, .. duplicateBiomesInRequest];

        if (groupedErrors.Length != 0 || duplicateErrors.Length != 0)
        {
            return ServiceResultBase.Fail(groupedErrors.Concat(duplicateErrors));
        }

        return ServiceResultBase.Succeed();
    }
}
