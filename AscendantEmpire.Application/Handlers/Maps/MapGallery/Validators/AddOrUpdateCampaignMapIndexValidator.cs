using AscendantEmpire.Application.Data;
using AscendantEmpire.FrontendContracts.MapGallery;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery.Validators;

/// <summary>
/// Concrete validator for adding a Campaign Map
/// </summary>
internal sealed class AddCampaignMapValidator
    : AddOrUpdateCampaignMapIndexValidator<AddCampaignMapRequest>
{
    public AddCampaignMapValidator(AscendantEmpireContext ctx) : base(ctx) { }
}

/// <summary>
/// Concrete validator for updating a Campaign Map Index
/// </summary>
internal sealed class UpdateCampaignMapIndexValidator
    : AddOrUpdateCampaignMapIndexValidator<UpdateCampaignMapIndexRequest>
{
    public UpdateCampaignMapIndexValidator(AscendantEmpireContext ctx) : base(ctx) 
    {
        RuleFor(x => x.Id)
            .NotNull().WithMessage("Id is required for updates.")
            .GreaterThan(0).WithMessage("Id must be greater than 0 for updates.");
    }
}

/// <summary>
/// Validate the Add/Update Campaign Map request
/// </summary>
// Checks the following:
// ---------------------------------------------------------
// Description                                 Add  Update
// ---------------------------------------------------------    
// Id is required                              ✖    ✔
// MapName must be unique                      ✔    ✔
// MapName required, max 50 chars              ✔    ✔
// Width/Height validated                      ✔    ✔
// CreationEngine must be valid                ✔    ✔
// Description text optional, max 500 chars    ✔    ✔
internal class AddOrUpdateCampaignMapIndexValidator<T>
    : AbstractValidator<T> where T : ICampaignMapIndexUpsertRequest
{
    const int MIN_WIDTH = 10;
    const int MIN_HEIGHT = 10;
    const int MAX_WIDTH = 600;
    const int MAX_HEIGHT = 375;

    public AddOrUpdateCampaignMapIndexValidator(AscendantEmpireContext context)
    {
        RuleFor(x => x.MapName).NotEmpty().MaximumLength(50).WithMessage("Map name is required and must be 50 characters or less.")
            // Unique map name (exclude current record when updating)
            .MustAsync(async (request, name, cancel) =>
            {
                string trimmedMapName = name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(trimmedMapName))
                {
                    return false;
                }

                int currentId = request.Id ?? 0;

                return !await context.CampaignMapGalleryIndex
                    .AnyAsync(m => m.MapName == trimmedMapName && m.Id != currentId, cancel);
            })
            .WithMessage("A campaign map with this name already exists.");

        RuleFor(x => x.Description).MaximumLength(500).WithMessage("Description, if provided, must be 500 characters or less.");
        RuleFor(x => x.Width).InclusiveBetween(MIN_WIDTH, MAX_WIDTH).WithMessage($"Width must be between {MIN_WIDTH} and {MAX_WIDTH}.");
        RuleFor(x => x.Height).InclusiveBetween(MIN_HEIGHT, MAX_HEIGHT).WithMessage($"Height must be between {MIN_HEIGHT} and {MAX_HEIGHT}.");
        RuleFor(x => x.CreationEngine).IsInEnum().WithMessage("Invalid creation engine.");
    }
}
