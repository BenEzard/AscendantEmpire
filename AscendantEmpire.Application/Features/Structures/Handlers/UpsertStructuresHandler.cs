using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Structures;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Structures.Handlers;

public record UpsertStructuresRequest(IReadOnlyCollection<UpsertStructureDto> Structures) : IRequest<ServiceResultBase>;

/// <summary>
/// Creates or updates rows in the <c>structure.Structures</c> catalogue, matching on Code.
/// </summary>
/// <remarks>
/// <para>
/// The same shape as <c>UpsertExperienceTypesHandler</c>: validate the whole request first, load the existing rows in one query, then write.
/// </para>
/// </remarks>
internal class UpsertStructuresHandler(
    ILogger<UpsertStructuresHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<UpsertStructuresRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(UpsertStructuresRequest request, CancellationToken cancellationToken)
    {
        if (request.Structures.Count == 0)
        {
            return Fail("At least one structure must be provided.");
        }

        // Request-level validation, before any database work.
        List<string> errors = [];

        string[] blankCodes = [.. request.Structures
            .Where(structure => string.IsNullOrWhiteSpace(structure.Code))
            .Select(structure => structure.Name)];

        if (blankCodes.Length > 0)
        {
            errors.Add($"Structure code is required, and is missing for: {string.Join(", ", blankCodes)}");
        }

        string[] duplicateCodes = [.. request.Structures
            .GroupBy(structure => structure.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];

        if (duplicateCodes.Length > 0)
        {
            errors.Add($"Duplicate structure code(s): {string.Join(", ", duplicateCodes)}");
        }

        // Negative work would make the construction process run backwards, and the column has no check constraint to catch it. Zero is allowed: it means a Structure that
        // needs no building and is finished at the end of the Turn it was founded in.
        string[] negativeBuildPoints = [.. request.Structures
            .Where(structure => structure.BuildPointsRequired < 0)
            .Select(structure => $"{structure.Code} ({structure.BuildPointsRequired})")];

        if (negativeBuildPoints.Length > 0)
        {
            errors.Add($"Structure build points cannot be negative: {string.Join(", ", negativeBuildPoints)}");
        }

        if (errors.Count > 0)
        {
            logger.LogWarning("Validation failed for {Request}: {Errors}", nameof(UpsertStructuresRequest), string.Join("; ", errors));

            return Fail(errors);
        }

        // Load the existing rows in one query
        string[] codes = [.. request.Structures.Select(structure => structure.Code)];

        Dictionary<string, Structure> existing = await context.Structures
            .Where(structure => codes.Contains(structure.Code))
            .ToDictionaryAsync(structure => structure.Code, cancellationToken);

        // Upsert
        foreach (UpsertStructureDto dto in request.Structures)
        {
            if (!existing.TryGetValue(dto.Code, out Structure? entity))
            {
                entity = new Structure
                {
                    Code = dto.Code,
                    Name = dto.Name,
                    RecordTypeId = (int)RecordType.Structure, // Every row in this catalogue is a Structure; the column exists so the map projections can flatten structures and forces into one shape.
                    RequiresSlotType = dto.RequiresSlotType,
                    AllowedPositions = dto.AllowedPositions,
                    CanBeBuiltOnCampaignMap = dto.CanBeBuiltOnCampaignMap,
                    CanBeBuiltOnBattleMap = dto.CanBeBuiltOnBattleMap,
                    DualMapBuildTrigger = dto.DualMapBuildTrigger,
                    BuildPointsRequired = dto.BuildPointsRequired,
                    SightRange = dto.SightRange,
                    SightDirection = dto.SightDirection,
                    MapImagePath = dto.MapImagePath,
                    FullImagePath = dto.FullImagePath
                };

                context.Structures.Add(entity);
                existing[dto.Code] = entity;

                continue;
            }

            // Everything but the Code is overwritten.
            entity.Name = dto.Name;
            entity.RecordTypeId = (int)RecordType.Structure;
            entity.RequiresSlotType = dto.RequiresSlotType;
            entity.AllowedPositions = dto.AllowedPositions;
            entity.CanBeBuiltOnCampaignMap = dto.CanBeBuiltOnCampaignMap;
            entity.CanBeBuiltOnBattleMap = dto.CanBeBuiltOnBattleMap;
            entity.DualMapBuildTrigger = dto.DualMapBuildTrigger;
            entity.BuildPointsRequired = dto.BuildPointsRequired;
            entity.SightRange = dto.SightRange;
            entity.SightDirection = dto.SightDirection;
            entity.MapImagePath = dto.MapImagePath;
            entity.FullImagePath = dto.FullImagePath;
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Upserted {Count} structure(s).", request.Structures.Count);

        return SuccessNoContent();
    }
}
