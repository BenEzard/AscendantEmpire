using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.BackendContracts.Technologies.GetTechnologies;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Technologies.Handlers;

public record GetTechnologiesRequest() : IRequest<ServiceResult<IReadOnlyCollection<CatalogueTechnology>>>;

/// <summary>
/// Returns the whole technology catalogue - every technology with its costs, prerequisites, grants and experience sources.
/// </summary>
/// <remarks>
/// <para>
/// One call rather than several. The tech tree cannot be drawn without the prerequisite edges, and the editor cannot show a technology without what it costs
/// and gives, so splitting it would only make the client reassemble what the server already has in hand. It is player-independent while technology
/// customisation is Identical, which is what makes it cacheable for the life of a game.
/// </para>
/// <para>
/// Grant targets are resolved to a code and display name here, because <c>TechnologyGrant.TargetId</c> points at a different table per target type and a
/// client could only resolve one by fetching all five catalogues. A target that no longer exists comes back with nulls rather than being hidden: the grant
/// carries no foreign key, so it can outlive what it pointed at, and a dangling grant is something the editor should show.
/// </para>
/// </remarks>
internal class GetTechnologiesHandler(
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetTechnologiesRequest, ServiceResult<IReadOnlyCollection<CatalogueTechnology>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<CatalogueTechnology>>> Handle(
        GetTechnologiesRequest request,
        CancellationToken cancellationToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Projected rather than loaded as an entity graph: the grants need resolving against five unrelated catalogues afterwards, so there is nothing
        // to gain from tracking, and Include-ing four child collections would multiply the rows out.
        var technologies = await context.Technologies
            .AsNoTracking()
            .OrderBy(technology => technology.Era)
            .ThenBy(technology => technology.SortOrder)
            .Select(technology => new
            {
                technology.Id,
                technology.Code,
                technology.DisplayName,
                technology.Description,
                technology.IconPath,
                technology.Era,
                technology.SortOrder,
                technology.TreeColumn,
                technology.TreeRow,
                technology.DirectedRequired,
                technology.ExperienceRequired,
                technology.DiffusionRequired,
                AcquisitionCosts = technology.AcquisitionCosts
                    .OrderBy(cost => cost.Asset.DisplayName)
                    .Select(cost => new CatalogueAcquisitionCost
                    {
                        AssetId = cost.AssetId,
                        AssetCode = cost.Asset.Code,
                        AssetDisplayName = cost.Asset.DisplayName,
                        AssetIconPath = cost.Asset.IconPath,
                        Quantity = cost.Quantity
                    })
                    .ToList(),
                Prerequisites = technology.Prerequisites
                    .OrderBy(prerequisite => prerequisite.PrerequisiteTechnology.DisplayName)
                    .Select(prerequisite => new CataloguePrerequisite
                    {
                        PrerequisiteTechnologyId = prerequisite.PrerequisiteTechnologyId,
                        PrerequisiteTechnologyCode = prerequisite.PrerequisiteTechnology.Code,
                        PrerequisiteTechnologyDisplayName = prerequisite.PrerequisiteTechnology.DisplayName,
                        PrerequisiteKind = prerequisite.PrerequisiteKind
                    })
                    .ToList(),
                ExperienceSources = technology.ExperienceTypes
                    .OrderBy(mapping => mapping.ExperienceType.SortOrder)
                    .Select(mapping => new CatalogueExperienceSource
                    {
                        ExperienceTypeId = mapping.ExperienceTypeId,
                        ExperienceTypeCode = mapping.ExperienceType.Code,
                        ExperienceTypeDisplayName = mapping.ExperienceType.DisplayName
                    })
                    .ToList(),
                RawGrants = technology.Grants
                    .Select(grant => new
                    {
                        grant.TargetType,
                        grant.TargetId,
                        grant.GrantEffect
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        GrantTargetNames targetNames = await TechnologyGrantTargetResolver.LoadAsync(
            context,
            [.. technologies.SelectMany(technology => technology.RawGrants).Select(grant => (grant.TargetType, grant.TargetId))],
            cancellationToken);

        List<CatalogueTechnology> catalogue = [.. technologies.Select(technology => new CatalogueTechnology
        {
            Id = technology.Id,
            Code = technology.Code,
            DisplayName = technology.DisplayName,
            Description = technology.Description,
            IconPath = technology.IconPath,
            Era = technology.Era,
            SortOrder = technology.SortOrder,
            TreeColumn = technology.TreeColumn,
            TreeRow = technology.TreeRow,
            DirectedRequired = technology.DirectedRequired,
            ExperienceRequired = technology.ExperienceRequired,
            DiffusionRequired = technology.DiffusionRequired,
            AcquisitionCosts = technology.AcquisitionCosts,
            Prerequisites = technology.Prerequisites,
            ExperienceSources = technology.ExperienceSources,
            Grants = [.. technology.RawGrants
                .Select(grant =>
                {
                    (string? code, string? displayName) = targetNames.Resolve(grant.TargetType, grant.TargetId);

                    return new CatalogueGrant
                    {
                        TargetType = grant.TargetType,
                        TargetId = grant.TargetId,
                        TargetCode = code,
                        TargetDisplayName = displayName,
                        GrantEffect = grant.GrantEffect
                    };
                })
                // Ordered after resolution rather than in the query, since the name being ordered by is not in the grant row.
                .OrderBy(grant => grant.TargetType)
                .ThenBy(grant => grant.TargetDisplayName)]
        })];

        return ServiceResult<IReadOnlyCollection<CatalogueTechnology>>.Succeed(catalogue);
    }
}
