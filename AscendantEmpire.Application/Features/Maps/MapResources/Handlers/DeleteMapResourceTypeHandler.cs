using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.MapResources;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Handlers;

public record DeleteMapResourceTypeRequest(string Code) : IRequest<ServiceResultBase>;

/// <summary>
/// Deletes a MapResourceType and everything it owns, as long as nothing outside the catalogue uses it.
/// </summary>
/// <remarks>
/// A type placed on a gallery map, or known to any game, is refused with a message saying where it is used, rather than left to fail on a foreign key. Its yields,
/// habitats, scenarios and layers go with it (they cascade).
/// </remarks>
internal class DeleteMapResourceTypeHandler(
    AscendantEmpireContext context
) : IRequestHandler<DeleteMapResourceTypeRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(DeleteMapResourceTypeRequest request, CancellationToken cancellationToken)
    {
        MapResourceType? type = await context.MapResourceTypes.SingleOrDefaultAsync(t => t.Code == request.Code, cancellationToken);

        if (type == null)
        {
            return ServiceResultBase.NotFound($"There is no MapResourceType '{request.Code}'.");
        }

        int placements = await context.CampaignMapGalleryResources.CountAsync(p => p.MapResourceTypeId == type.Id, cancellationToken);

        // Every game table that names a type. Any one of them means a game depends on it.
        bool usedByAGame = await context.MapResourceInstances.AnyAsync(i => i.MapResourceTypeId == type.Id, cancellationToken)
            || await context.PlayerMapResourceTypeKnowledge.AnyAsync(k => k.MapResourceTypeId == type.Id, cancellationToken)
            || await context.PlayerMapResourceModifiers.AnyAsync(m => m.MapResourceTypeId == type.Id, cancellationToken)
            || await context.PlayerMapResourceSwitches.AnyAsync(s => s.MapResourceTypeId == type.Id, cancellationToken);

        List<string> uses = [];

        if (placements != 0)
        {
            uses.Add($"it is placed on {placements} map tile(s)");
        }

        if (usedByAGame)
        {
            uses.Add("a game in progress uses it");
        }

        if (uses.Count != 0)
        {
            return ServiceResultBase.Fail($"{type.Code} cannot be deleted: {string.Join(", and ", uses)}.");
        }

        context.MapResourceTypes.Remove(type);
        await context.SaveChangesAsync(CancellationToken.None);

        return ServiceResultBase.SuccessNoContent();
    }
}
