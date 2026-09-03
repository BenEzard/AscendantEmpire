using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.FrontendContracts.MapGallery;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Validators;

/// <summary>
/// Concrete validator for adding a Campaign Map
/// </summary>
internal sealed class AddCampaignMapValidator : AddOrUpdateCampaignMapIndexValidator<AddCampaignMapRequest>;

/// <summary>
/// Concrete validator for updating a Campaign Map Index (name and description only).
/// </summary>
internal sealed class UpdateCampaignMapIndexValidator : Validator<UpdateCampaignMapIndexRequest>
{
    public UpdateCampaignMapIndexValidator()
    {
        RuleFor(x => x.Id)
            .NotNull().WithMessage("Id is required for updates.")
            .GreaterThan(0).WithMessage("Id must be greater than 0 for updates.");

        RuleFor(x => x.MapName)
            .NotEmpty().MaximumLength(50).WithMessage("Map name is required and must be 50 characters or less.");
    }
}

/// <summary>
/// Validate the Add Campaign Map request
/// </summary>
/// <remarks>
/// <para>Map name uniqueness is not checked here. It is the one rule that has to ask the database, and a validator is built once and shared by every
/// request, so the check lives in the handlers instead — beside every other existence check in this codebase, and on the path any caller takes rather than
/// only the one that goes through the request pipeline.</para>
/// </remarks>
// Checks the following:
// ---------------------------------------------------------
// Description                                 Add  Update
// ---------------------------------------------------------
// Id is required                              ✖    ✔
// MapName required, max 50 chars              ✔    ✔
// Width/Height validated                      ✔    ✖
// CreationEngine must be valid                ✔    ✖
// Description text optional, no constraints  ✔    ✔
//
// MapName uniqueness is checked by the handlers, not here — see the remarks above.
internal class AddOrUpdateCampaignMapIndexValidator<T>
    : Validator<T> where T : ICampaignMapIndexUpsertRequest
{
    const int MIN_WIDTH = 10;
    const int MIN_HEIGHT = 10;
    const int MAX_WIDTH = 600;
    const int MAX_HEIGHT = 375;

    public AddOrUpdateCampaignMapIndexValidator()
    {
        RuleFor(x => x.MapName).NotEmpty().MaximumLength(50).WithMessage("Map name is required and must be 50 characters or less.");

        RuleFor(x => x.Width).InclusiveBetween(MIN_WIDTH, MAX_WIDTH).WithMessage($"Width must be between {MIN_WIDTH} and {MAX_WIDTH}.");
        RuleFor(x => x.Height).InclusiveBetween(MIN_HEIGHT, MAX_HEIGHT).WithMessage($"Height must be between {MIN_HEIGHT} and {MAX_HEIGHT}.");
        RuleFor(x => x.CreationEngine).IsInEnum().WithMessage("Invalid creation engine.");
    }
}
