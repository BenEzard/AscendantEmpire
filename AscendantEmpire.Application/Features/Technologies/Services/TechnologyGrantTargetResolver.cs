using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <summary>
/// Resolves what a technology's grants point at into codes and display names.
/// </summary>
/// <remarks>
/// <para>
/// <c>TechnologyGrant.TargetId</c> carries no foreign key, because it points at a different table for each target type, so a client could only resolve one
/// by fetching all five catalogues. The server resolves them instead - and does so here rather than inside one handler, because both the catalogue read and
/// the per-Player research read serve grants and a second copy of this is a second place for the five target types to fall out of step.
/// </para>
/// <para>
/// A target that no longer exists resolves to nulls rather than being hidden. The grant can outlive what it pointed at, and a dangling grant is something a
/// reader should see rather than something to quietly drop.
/// </para>
/// </remarks>
internal static class TechnologyGrantTargetResolver
{
    /// <summary>
    /// Loads the code and display name of every named grant target, one query per catalogue that is actually referenced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Narrowed to the ids in play rather than loading five whole catalogues, and skipped entirely for a target type nothing grants - which, for a catalogue
    /// that grants nothing yet, means no extra queries at all.
    /// </para>
    /// <para>
    /// Four of the five selectors cast <c>Code</c> to <see langword="string"/>? on the way out. Only <c>UnitTemplate.Code</c> is genuinely nullable, but
    /// <see cref="Dictionary{TKey, TValue}"/> is invariant in its value, so a dictionary of non-nullable codes is not the same type as one of nullable codes
    /// and cannot be passed as it (CS8620). Widening at the selector keeps all five the one type the switch in <c>Resolve</c> needs.
    /// </para>
    /// </remarks>
    public static async Task<GrantTargetNames> LoadAsync(
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
                    .ToDictionaryAsync(asset => asset.Id, asset => ((string?)asset.Code, asset.DisplayName), cancellationToken),
            AssetMetaCategories: metaCategoryIds.Length == 0
                ? []
                : await context.AssetMetaCategories
                    .AsNoTracking()
                    .Where(metaCategory => metaCategoryIds.Contains(metaCategory.Id))
                    .ToDictionaryAsync(metaCategory => metaCategory.Id, metaCategory => ((string?)metaCategory.Code, metaCategory.DisplayName), cancellationToken),
            UnitComponents: componentIds.Length == 0
                ? []
                : await context.UnitComponents
                    .AsNoTracking()
                    .Where(component => componentIds.Contains(component.Id))
                    .ToDictionaryAsync(component => component.Id, component => ((string?)component.Code, component.DisplayName), cancellationToken),
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
                        mapResourceType => ((string?)mapResourceType.Code, mapResourceType.DisplayName),
                        cancellationToken));
    }
}

/// <summary>
/// Id-to-name maps for everything a grant can point at.
/// </summary>
internal sealed record GrantTargetNames(
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
