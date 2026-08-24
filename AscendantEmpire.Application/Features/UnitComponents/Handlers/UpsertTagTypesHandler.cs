using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTagType;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Bulk-upserts the tag type catalogue from the dev Unit Designer's tag type manager: every posted row is
/// created (if its enum key is missing) or updated in place. Mirrors the campaign map biome upsert flow.
/// </summary>
/// <remarks>
/// The tag type set is fixed by the <see cref="TagType"/> enum, so there is no per-item create/delete —
/// the manager loads the whole set, edits it, and saves it back in one request.
/// </remarks>
public record UpsertTagTypesRequest(IReadOnlyCollection<UpsertTagTypeDto> TagTypes) : IRequest<ServiceResultBase>;

internal class UpsertTagTypesHandler(
    ILogger<UpsertTagTypesHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<UpsertTagTypesRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(UpsertTagTypesRequest request, CancellationToken cancelToken)
    {
        try
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

            if (errors.Count > 0)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(UpsertTagTypesRequest), string.Join("; ", errors));
                return ServiceResultBase.Fail(errors);
            }

            // Pull the rows that already exist so we can update them; anything else is a fresh insert.
            List<TagType> requestedKeys = [.. request.TagTypes.Select(t => t.TagTypeId)];

            Dictionary<TagType, TagTypeInfo> existingByKey = await context.TagTypeInfos
                .Where(t => requestedKeys.Contains(t.TagTypeId))
                .ToDictionaryAsync(t => t.TagTypeId, cancelToken);

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
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(UpsertTagTypesHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResultBase.InternalServerError(error);
        }
    }
}
