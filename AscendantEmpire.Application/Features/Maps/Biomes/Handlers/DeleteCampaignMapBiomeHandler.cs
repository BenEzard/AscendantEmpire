using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

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
    AscendantEmpireContext context
    ) : IRequestHandler<DeleteCampaignMapBiomeRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(DeleteCampaignMapBiomeRequest request, CancellationToken cancelToken)
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
}
