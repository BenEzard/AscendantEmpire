using FluentValidation;
using FluentValidation.Results;
using AscendantEmpire.Application.Common;

using MediatR;
using AscendantEmpire.Application.Data;
using Microsoft.Extensions.Logging;
using static AscendantEmpire.Application.Common.ServiceResultBase;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.FrontendContracts.Biomes;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.Application.Handlers.Maps.Biomes.Validator;

namespace AscendantEmpire.Application.Handlers.Maps.Biomes;

public record UpdateCampaignMapBiomesRequest(IReadOnlyCollection<AddOrUpdateCampaignMapBiomeDto> Biomes) : IRequest<ServiceResultBase>;

internal class UpdateCampaignMapBiomesHandler(
    ILogger<UpdateCampaignMapBiomesHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<UpdateCampaignMapBiomesRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(UpdateCampaignMapBiomesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate the request objects
            ServiceResultBase validation = ValidateRequestBiomes(context, request.Biomes);

            if (validation.HasSucceeded == false)
            {
                logger.LogWarning("Validation failed for " + nameof(UpdateCampaignMapBiomesRequest) + ": {Errors}", string.Join(Environment.NewLine, validation.Errors));
                return ServiceResult.Fail(validation.Errors);
            }

            Dictionary<BiomeType, AddOrUpdateCampaignMapBiomeDto> requestedUpdates = request.Biomes.ToDictionary(b => b.BiomeType);

            CampaignBiome[] existingBiomes = [.. context.CampaignBiomes.Where(b => request.Biomes.Select(rb => rb.BiomeType).Contains((BiomeType)b.BiomeTypeId))];

            foreach (CampaignBiome existingBiome in existingBiomes)
            {
                AddOrUpdateCampaignMapBiomeDto updatedValues = requestedUpdates[(BiomeType)existingBiome.BiomeTypeId];

                existingBiome.Name = updatedValues.Name;
                existingBiome.MovementCost = updatedValues.MovementCost;
                existingBiome.DoesBlockSight = updatedValues.DoesBlockSight;
                existingBiome.IsWater = updatedValues.IsWater;
                existingBiome.IsTraversable = updatedValues.IsTraversable;
                existingBiome.RegenerationRate = updatedValues.RegenerationRate;
                existingBiome.BaseForageValue = updatedValues.BaseForageValue;
                existingBiome.BaseHuntValue = updatedValues.BaseHuntValue;
                existingBiome.BaseFishValue = updatedValues.BaseFishValue;
                existingBiome.BaseFodderValue = updatedValues.BaseFodderValue;
                existingBiome.BaseBoneValue = updatedValues.BaseBoneValue;
                existingBiome.BaseWoodValue = updatedValues.BaseWoodValue;
                existingBiome.BaseStoneValue = updatedValues.BaseStoneValue;
                existingBiome.BaseSpecialValue = updatedValues.BaseSpecialValue;
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
    /// <remarks>Checks there isn't already a record with the same BiomeTypeId in the table</remarks>
    private static ServiceResultBase ValidateRequestBiomes(AscendantEmpireContext context, IReadOnlyCollection<IAddOrUpdateCampaignMapBiomeDto> biomes)
    {
        var validator = new CampaignMapBiomeValidator();

        // Validate individual Biomes
        (BiomeType BiomeType, ValidationResult ValidationResults)[] validationResults = biomes
            .Select(biome => (biome.BiomeType, validator.Validate(biome)))
            .ToArray();

        string[] groupedErrors = validationResults
            .Where(result => result.ValidationResults.Errors.Count != 0)
            .SelectMany(result =>
                result.ValidationResults.Errors.Select(error =>
                    $"Biome '{result.BiomeType}': {error.ErrorMessage}"))
            .ToArray();

        // Check the Biome Type Id IS already in the database
        BiomeType[] biomeTypeIdsInRequest = [.. biomes.Select(b => b.BiomeType)];
        string[] biomesMissingFromDb = biomeTypeIdsInRequest
            .Where(requestedId => context.CampaignBiomes
                .Any(db => db.BiomeTypeId == requestedId) == false)
            .Select(id => $"{id} is not a Biome. Has it been created yet?")
            .ToArray();

        // Check for any duplicate BiomeTypeIds in the request object
        string[] duplicateBiomesInRequest = biomes
            .GroupBy(b => b.BiomeType)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} is specified multiple times in the request.")
            .ToArray();

        if (groupedErrors.Length != 0 || duplicateBiomesInRequest.Length != 0)
        {
            return ServiceResultBase.Fail(groupedErrors.Concat(duplicateBiomesInRequest));
        }

        else if (biomesMissingFromDb.Length != 0)
        {
            return ServiceResultBase.NotFound(biomesMissingFromDb);
        }

        return ServiceResultBase.Succeed();
    }
}
