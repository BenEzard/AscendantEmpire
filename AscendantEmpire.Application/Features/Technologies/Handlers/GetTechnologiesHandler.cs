using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Technologies.GetTechnologies;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    ILogger<GetTechnologiesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetTechnologiesRequest, ServiceResult<IReadOnlyCollection<CatalogueTechnology>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<CatalogueTechnology>>> Handle(
        GetTechnologiesRequest request,
        CancellationToken cancellationToken)
    {
        try
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

            GrantTargetNames targetNames = await LoadGrantTargetNamesAsync(
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
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving technologies.";
            logger.LogError(ex, error);

            return ServiceResult<IReadOnlyCollection<CatalogueTechnology>>.Fail(error);
        }
    }

    /// <summary>
    /// Loads the code and display name of every grant target, one query per catalogue that is actually referenced.
    /// </summary>
    /// <remarks>
    /// Narrowed to the ids in play rather than loading five whole catalogues, and skipped entirely for a target type nothing grants - which, for a catalogue
    /// that grants nothing yet, means no extra queries at all.
    /// </remarks>
    private static async Task<GrantTargetNames> LoadGrantTargetNamesAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<(TechnologyGrantTargetType TargetType, int TargetId)> targets,
        CancellationToken cancellationToken)
    {
        int[] IdsFor(TechnologyGrantTargetType targetType) =>
            [.. targets.Where(target => target.TargetType == targetType).Select(target => target.TargetId).Distinct()];

        int[] assetIds = IdsFor(TechnologyGrantTargetType.Asset);
        int[] metaCategoryIds = IdsFor(TechnologyGrantTargetType.AssetMetaCategory);
        int[] componentIds = IdsFor(TechnologyGrantTargetType.UnitComponent);
        int[] templateIds = IdsFor(TechnologyGrantTargetType.UnitTemplate);
        int[] mapResourceTypeIds = IdsFor(TechnologyGrantTargetType.MapResourceType);

        return new GrantTargetNames(
            Assets: assetIds.Length == 0
                ? []
                : await context.Assets
                    .AsNoTracking()
                    .Where(asset => assetIds.Contains(asset.Id))
                    .ToDictionaryAsync(asset => asset.Id, asset => (asset.Code, asset.DisplayName), cancellationToken),
            AssetMetaCategories: metaCategoryIds.Length == 0
                ? []
                : await context.AssetMetaCategories
                    .AsNoTracking()
                    .Where(metaCategory => metaCategoryIds.Contains(metaCategory.Id))
                    .ToDictionaryAsync(metaCategory => metaCategory.Id, metaCategory => (metaCategory.Code, metaCategory.DisplayName), cancellationToken),
            UnitComponents: componentIds.Length == 0
                ? []
                : await context.UnitComponents
                    .AsNoTracking()
                    .Where(component => componentIds.Contains(component.Id))
                    .ToDictionaryAsync(component => component.Id, component => (component.Code, component.DisplayName), cancellationToken),
            // UnitTemplate.Code is nullable, so a template without one resolves to a name but no code.
            UnitTemplates: templateIds.Length == 0
                ? []
                : await context.UnitTemplates
                    .AsNoTracking()
                    .Where(template => templateIds.Contains(template.Id))
                    .ToDictionaryAsync(template => template.Id, template => (template.Code, template.DisplayName), cancellationToken),
            MapResourceTypes: mapResourceTypeIds.Length == 0
                ? []
                : await context.MapResourceTypes
                    .AsNoTracking()
                    .Where(mapResourceType => mapResourceTypeIds.Contains(mapResourceType.Id))
                    .ToDictionaryAsync(
                        mapResourceType => mapResourceType.Id,
                        mapResourceType => (mapResourceType.Code, mapResourceType.DisplayName),
                        cancellationToken));
    }

    /// <summary>
    /// Id-to-name maps for everything a grant can point at.
    /// </summary>
    private sealed record GrantTargetNames(
        Dictionary<int, (string? Code, string DisplayName)> Assets,
        Dictionary<int, (string? Code, string DisplayName)> AssetMetaCategories,
        Dictionary<int, (string? Code, string DisplayName)> UnitComponents,
        Dictionary<int, (string? Code, string DisplayName)> UnitTemplates,
        Dictionary<int, (string? Code, string DisplayName)> MapResourceTypes)
    {
        /// <summary>
        /// The target's code and display name, or nulls when it no longer exists.
        /// </summary>
        public (string? Code, string? DisplayName) Resolve(TechnologyGrantTargetType targetType, int targetId)
        {
            Dictionary<int, (string? Code, string DisplayName)> lookup = targetType switch
            {
                TechnologyGrantTargetType.Asset => Assets,
                TechnologyGrantTargetType.AssetMetaCategory => AssetMetaCategories,
                TechnologyGrantTargetType.UnitComponent => UnitComponents,
                TechnologyGrantTargetType.UnitTemplate => UnitTemplates,
                TechnologyGrantTargetType.MapResourceType => MapResourceTypes,
                _ => []
            };

            return lookup.TryGetValue(targetId, out (string? Code, string DisplayName) found)
                ? (found.Code, found.DisplayName)
                : (null, null);
        }
    }
}
