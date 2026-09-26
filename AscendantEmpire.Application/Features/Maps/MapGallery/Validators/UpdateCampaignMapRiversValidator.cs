using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapRivers;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Validators;

/// <summary>
/// Validates the shape of a request to set river edges: that it asks for something, and that everything it asks for is a value this codebase knows.
/// </summary>
/// <remarks>
/// <para>
/// Only the rules that can be decided from the request alone live here. Whether a coordinate is on the map, whether an edge faces off a non-wrapping
/// boundary, whether it has sea on both sides, whether it reaches the coast and whether it widens inland all need the map, so they belong in
/// <see cref="UpdateCampaignMapRiversHandler"/> and <see cref="Classes.RiverNetworkRules"/>. A validator is a shared singleton and holds no
/// dependencies.
/// </para>
/// <para>
/// Negative coordinates are refused here rather than in the handler because they are a malformed request rather than a rejected edit: no map has a
/// column -1, so there is nothing to look up before saying so.
/// </para>
/// </remarks>
internal sealed class UpdateCampaignMapRiversValidator : Validator<UpdateCampaignMapRiversRequest>
{
    public UpdateCampaignMapRiversValidator()
    {
        RuleFor(x => x.CampaignMapGalleryIndexId)
            .GreaterThan(0).WithMessage("A map id is required.");

        RuleFor(x => x.Changes)
            .NotEmpty().WithMessage("No river edges were supplied.");

        RuleFor(x => x.Changes)
            .Must(changes => changes is null || changes.All(change => change.X >= 0 && change.Y >= 0))
            .WithMessage("Coordinates cannot be negative.");

        RuleFor(x => x.Changes)
            .Must(changes => changes is null || changes.All(change => Enum.IsDefined(change.Edge)))
            .WithMessage("An edge must be South or East.");

        RuleFor(x => x.Changes)
            .Must(changes => changes is null || changes.All(change => Enum.IsDefined(change.Width)))
            .WithMessage("A width must be None, Medium or Large.");

        // The same edge twice in one batch has no defined answer — the two changes would both be applied and the last one silently win, so the author
        // would be told their river is one width while the map holds another.
        RuleFor(x => x.Changes)
            .Must(changes => changes is null || changes.DistinctBy(change => (change.X, change.Y, change.Edge)).Count() == changes.Count)
            .WithMessage("The same edge was listed more than once.");
    }
}
