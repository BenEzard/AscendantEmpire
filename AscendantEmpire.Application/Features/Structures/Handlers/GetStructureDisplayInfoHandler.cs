using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Structures.GetStructureDisplayInfo;
using AscendantEmpire.Domain.Entities.Structures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Structures.Handlers;

/// <summary>
/// Asks for the display information of every kind of Structure that can stand on the campaign map. Takes nothing: the catalogue is the same for every Game.
/// </summary>
public record GetStructureDisplayInfoRequest() : IRequest<ServiceResult<IReadOnlyCollection<StructureDisplayInfo>>>;

/// <summary>
/// The name and map image of every kind of Structure that can be built on the campaign map, so the client can name a Structure an intelligence report only identifies by id.
/// </summary>
/// <remarks>
/// <para>
/// The Structure counterpart of <c>GetUnitDisplayInfoHandler</c>, but not scoped to a Game: unit templates can be a Player's own, while Structure kinds are a global catalogue that every Player
/// shares.
/// </para>
/// <para>
/// Battle-map-only kinds are left out. Nothing on a battle map reaches the campaign map or its intelligence, so the client never has one to name.
/// </para>
/// </remarks>
internal class GetStructureDisplayInfoHandler(
    ILogger<GetStructureDisplayInfoHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetStructureDisplayInfoRequest, ServiceResult<IReadOnlyCollection<StructureDisplayInfo>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<StructureDisplayInfo>>> Handle(GetStructureDisplayInfoRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // The context exposes no DbSet for the catalogue, so it is reached through Set<Structure>().
            List<StructureDisplayInfo> structureDisplayInfo = await context.Set<Structure>()
                .AsNoTracking()
                .Where(structure => structure.CanBeBuiltOnCampaignMap)
                .OrderBy(structure => structure.Id)
                .Select(structure => new StructureDisplayInfo
                {
                    StructureId = structure.Id,
                    Name = structure.Name,
                    MapImagePath = structure.MapImagePath
                })
                .ToListAsync(cancelToken);

            return ServiceResult<IReadOnlyCollection<StructureDisplayInfo>>.Succeed(structureDisplayInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(GetStructureDisplayInfoRequest));

            return ServiceResult<IReadOnlyCollection<StructureDisplayInfo>>.Fail("An unexpected error occurred while retrieving structure display information.");
        }
    }
}
