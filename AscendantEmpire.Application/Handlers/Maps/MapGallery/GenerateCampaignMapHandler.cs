using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using FluentValidation;
using FluentValidation.Results;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery;

public record GenerateCampaignMapRequest(
    string MapName,
    string Description,
    int Width,
    int Height,
    MapCreationEngine CreationEngine,
    Dictionary<string, string>? Parameters
) : IRequest<ServiceResult<int>>;

/// <summary>
/// Validate the Generate Campaign Map request
/// </summary>
/// <remarks>Checks the following:
/// <list type="bullet">
/// <item>MapName is not empty</item>
/// <item>Width is between 10 and 400</item>
/// <item>Height is between 10 and 300</item>
/// <item>CreationStatus is a valid enum value</item>
/// <item>CreationEngine is a valid enum value</item>
/// </list>
/// </remarks>
internal class GenerateCampaignMapValidator : AbstractValidator<GenerateCampaignMapRequest>
{
    const int MIN_WIDTH = 10;
    const int MIN_HEIGHT = 10;
    const int MAX_WIDTH = 600;
    const int MAX_HEIGHT = 375;

    public GenerateCampaignMapValidator()
    {
        RuleFor(x => x.MapName).NotEmpty().MaximumLength(50).WithMessage("Map name is required and must be 50 characters or less.");
        RuleFor(x => x.Description).MaximumLength(500).WithMessage("Description, if provided, must be 500 characters or less.");
        RuleFor(x => x.Width).InclusiveBetween(MIN_WIDTH, MAX_WIDTH).WithMessage($"Width must be between {MIN_WIDTH} and {MAX_WIDTH}.");
        RuleFor(x => x.Height).InclusiveBetween(MIN_HEIGHT, MAX_HEIGHT).WithMessage($"Height must be between {MIN_HEIGHT} and {MAX_HEIGHT}.");
        RuleFor(x => x.CreationEngine).IsInEnum().WithMessage("Invalid creation engine.");
    }
}

internal class GenerateCampaignMapHandler(
    ILogger<GenerateCampaignMapHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<GenerateCampaignMapRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(GenerateCampaignMapRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate the request objects
            var validator = new GenerateCampaignMapValidator();

            ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
            
            if (validationResult.IsValid == false)
            {
                logger.LogWarning("Validation failed for " + nameof(GenerateCampaignMapRequest) + ": {Errors}", string.Join(Environment.NewLine, validationResult.Errors.Select(e => e.ErrorMessage)));
                return ServiceResult<int>.Fail(validationResult.Errors.Select(e => e.ErrorMessage));
            }

            // Get how many Campaign Maps are in Pending status
            int pendingMapsCount = await context.CampaignMapGalleryIndex.CountAsync(x => x.CreationStatus == MapCreationStatus.Pending, cancellationToken);

            // Insert a Campaign Map Index record
            var newIndexRecord = new CampaignMapGalleryIndex
            {
                MapName = request.MapName,
                Description = request.Description,
                Width = request.Width,
                Height = request.Height,
                CreationStatus = MapCreationStatus.Pending,
                CreationEngine = request.CreationEngine,
                Status = $"Pending creation (#{pendingMapsCount + 1} in queue)",
                ErrorMessage = string.Empty,
                PropertiesJson = request.Parameters != null ? System.Text.Json.JsonSerializer.Serialize(request.Parameters) : string.Empty,
                CampaignMapOffsetId = -1 // Set a default value (to be updated later when the map is created)
            };

            context.CampaignMapGalleryIndex.Add(newIndexRecord);
            await context.SaveChangesAsync(cancellationToken);

            return ServiceResult<int>.Created(newIndexRecord.Id);
        }
        catch (Exception ex)
        {
            const string error = "Error creating Campaign Map Gallery Index record.";
            logger.LogError(ex, error);
            return ServiceResult<int>.InternalServerError(error);
        }
    }
}
