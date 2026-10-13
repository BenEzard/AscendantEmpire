using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTagType;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Bulk-upserts the tag type catalogue from the dev Unit Designer's tag type manager: every posted row is
/// created (if its enum key is missing) or updated in place. Mirrors the campaign map biome upsert flow.
/// </summary>
/// <remarks>
/// <para>
/// The tag type set is fixed by the <see cref="TagType"/> enum, so there is no per-item create/delete —
/// the manager loads the whole set, edits it, and saves it back in one request.
/// </para>
/// <para>
/// Colours must be unique across the catalogue: the request is checked both against itself and against the
/// tag types it does not mention. Uncoloured rows are exempt.
/// </para>
/// </remarks>
public record UpsertTagTypesRequest(IReadOnlyCollection<UpsertTagTypeDto> TagTypes) : IRequest<ServiceResultBase>;

internal class UpsertTagTypesHandler(
    ILogger<UpsertTagTypesHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<UpsertTagTypesRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(UpsertTagTypesRequest request, CancellationToken cancelToken)
    {
        // Shape-validate every row first, so a malformed request never touches the database.
        List<string> errors = [.. request.TagTypes.SelectMany(TagTypeValidation.Validate)];

        // A duplicated key in the request would make the upsert ambiguous — reject it up front.
        List<TagType> duplicateKeys = [.. request.TagTypes
            .GroupBy(t => t.TagTypeId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        foreach (TagType duplicate in duplicateKeys)
        {
            errors.Add($"Tag type '{duplicate}' is specified multiple times in the request.");
        }

        // A colour identifies a tag type at a glance wherever tags are presented, so two tag types sharing
        // one would be ambiguous. Rows with no colour are exempt — any number of them may go uncoloured.
        // Colours are compared as trimmed, case-insensitive strings: "#FF0000" and "#ff0000" collide, but
        // equivalent notations of the same colour ("#ff0000" vs "red") are not resolved, as the backend has
        // no CSS colour parser. The picker in the Tag Type Setup dialog always posts hex.
        Dictionary<string, List<TagType>> requestedByColour = request.TagTypes
            .Where(t => !string.IsNullOrWhiteSpace(t.Colour))
            .GroupBy(t => t.Colour!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(t => t.TagTypeId).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach ((string colour, List<TagType> claimants) in requestedByColour.Where(pair => pair.Value.Count > 1))
        {
            errors.Add($"Colour '{colour}' is used by more than one tag type in the request: {string.Join(", ", claimants)}.");
        }

        if (errors.Count > 0)
        {
            logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(UpsertTagTypesRequest), string.Join("; ", errors));
            return ServiceResultBase.Fail(errors);
        }

        HashSet<TagType> requestedKeys = [.. request.TagTypes.Select(t => t.TagTypeId)];

        // The table is bounded by the TagType enum, so loading the whole catalogue costs nothing and lets the
        // colour check below see rows this request does not mention. Requested rows are updated in place;
        // anything else in the request is a fresh insert.
        List<TagTypeInfo> allExisting = await context.TagTypeInfos.ToListAsync(cancelToken);

        Dictionary<TagType, TagTypeInfo> existingByKey = allExisting
            .Where(t => requestedKeys.Contains(t.TagTypeId))
            .ToDictionary(t => t.TagTypeId);

        // A colour held by a tag type the request leaves alone is still taken. Without this, a partial save
        // could hand one tag type a colour another already owns.
        foreach (TagTypeInfo untouched in allExisting.Where(t => !requestedKeys.Contains(t.TagTypeId)))
        {
            if (string.IsNullOrWhiteSpace(untouched.Colour))
            {
                continue;
            }

            string existingColour = untouched.Colour.Trim();

            if (requestedByColour.TryGetValue(existingColour, out List<TagType>? claimants))
            {
                errors.Add($"Colour '{existingColour}' is already used by tag type '{untouched.TagTypeId}', so it cannot also be given to {string.Join(", ", claimants)}.");
            }
        }

        if (errors.Count > 0)
        {
            logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(UpsertTagTypesRequest), string.Join("; ", errors));
            return ServiceResultBase.Fail(errors);
        }

        foreach (UpsertTagTypeDto dto in request.TagTypes)
        {
            if (existingByKey.TryGetValue(dto.TagTypeId, out TagTypeInfo? existing))
            {
                existing.DisplayName = dto.DisplayName;
                existing.Description = dto.Description;
                existing.IsInheritedToBase = dto.IsInheritedToBase;
                existing.Colour = dto.Colour;
                existing.Icon = dto.Icon;
            }
            else
            {
                context.TagTypeInfos.Add(new TagTypeInfo
                {
                    TagTypeId = dto.TagTypeId,
                    DisplayName = dto.DisplayName,
                    Description = dto.Description,
                    IsInheritedToBase = dto.IsInheritedToBase,
                    Colour = dto.Colour,
                    Icon = dto.Icon,
                });
            }
        }

        await context.SaveChangesAsync(cancelToken);

        logger.LogInformation("Upserted {Count} tag type(s).", request.TagTypes.Count);

        return ServiceResultBase.SuccessNoContent();
    }
}
