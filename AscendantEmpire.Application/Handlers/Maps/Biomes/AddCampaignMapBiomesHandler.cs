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
                RegenerationRate = biome.RegenerationRate,
                BaseForageValue = biome.BaseForageValue,
                BaseHuntValue = biome.BaseHuntValue,
                BaseFishValue = biome.BaseFishValue,
                BaseFodderValue = biome.BaseFodderValue,
                BaseBoneValue = biome.BaseBoneValue,
                BaseWoodValue = biome.BaseWoodValue,
                BaseStoneValue = biome.BaseStoneValue,
                BaseSpecialValue = biome.BaseSpecialValue,
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
        (BiomeType BiomeType, ValidationResult ValidationResults)[] validationResults = biomes
            .Select(biome => (biome.BiomeType, validator.Validate(biome)))
            .ToArray();

        string[] groupedErrors = validationResults
            .Where(result => result.ValidationResults.Errors.Count != 0)
            .SelectMany(result =>
                result.ValidationResults.Errors.Select(error =>
                    $"Biome '{result.BiomeType}': {error.ErrorMessage}"))
            .ToArray();

        // Check the Biome Type Id isn't already in the database
        BiomeType[] biomeTypeIdsInRequest = biomes.Select(b => b.BiomeType).ToArray();
        string[] duplicateBiomesInDatabase = context.CampaignBiomes
            .Select(b => b.BiomeTypeId)
            .Where(id => biomeTypeIdsInRequest.Contains(id))
            .Select(id => $"{(BiomeType)id} is already a Biome. Either delete the existing Biome or edit it.")
            .ToArray();

        // Check for any duplicate BiomeTypeIds in the request object
        string[] duplicateBiomesInRequest = biomes
            .GroupBy(b => b.BiomeType)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} is specified multiple times in the request.")
            .ToArray();

        string[] duplicateErrors = duplicateBiomesInDatabase.Concat(duplicateBiomesInRequest).ToArray();

        if (groupedErrors.Length != 0 || duplicateErrors.Length != 0)
        {
            return ServiceResultBase.Fail(groupedErrors.Concat(duplicateErrors));
        }

        return ServiceResultBase.Succeed();
    }
}
