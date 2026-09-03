using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Handlers;

public record DeleteCampaignMapBiomeRequest(int BiomeTypeId) : IRequest<ServiceResultBase>;

internal class DeleteCampaignMapBiomeValidator : Validator<int>
{
    public DeleteCampaignMapBiomeValidator()
    {
        RuleFor(b => b)
            .Must(value => Enum.IsDefined(typeof(BiomeType), value))
            .WithMessage(b => "Must be a valid Biome Type value.");
    }
}

internal class DeleteCampaignMapBiomeHandler(
    ILogger<DeleteCampaignMapBiomeHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<DeleteCampaignMapBiomeRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(DeleteCampaignMapBiomeRequest request, CancellationToken cancelToken)
    {
        try
        {
            var validator = new DeleteCampaignMapBiomeValidator();

            var validationResult = validator.Validate(request.BiomeTypeId);
            if (validationResult.IsValid == false)
            {
                return ServiceResultBase.Fail([.. validationResult.Errors.Select(e => e.ErrorMessage)]);
            }

            BiomeType biomeType = (BiomeType)request.BiomeTypeId;
            CampaignBiome? existingBiome = await context.CampaignBiomes.SingleOrDefaultAsync(b => b.BiomeTypeId == biomeType, cancelToken);
            
            if (existingBiome == null)
            {
                return ServiceResultBase.NotFound($"Campaign Map Biome with Biome Type '{biomeType}' does not exist.");
            }
            
            context.CampaignBiomes.Remove(existingBiome);
            
            await context.SaveChangesAsync(cancelToken);
            
            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting Campaign Map Biome with Biome Type ID {BiomeTypeId}", request.BiomeTypeId);
            return ServiceResultBase.Fail("An error occurred while deleting the Campaign Map Biome.");
        }
    }
}
