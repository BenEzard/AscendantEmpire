using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Domain.Entities.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Technologies.Handlers;

public record UpsertExperienceTypesRequest(IReadOnlyCollection<UpsertExperienceTypeDto> ExperienceTypes) : IRequest<ServiceResultBase>;

/// <summary>
/// Creates or updates experience types, matching on Code.
/// </summary>
/// <remarks>
/// The same path the seed and the Entity Setup Tool both take, so seeding and hand-editing cannot diverge in how a row is matched or written.
/// </remarks>
internal class UpsertExperienceTypesHandler(
    ILogger<UpsertExperienceTypesHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<UpsertExperienceTypesRequest, ServiceResultBase>
{
    private const int _SORT_ORDER_STEP = 10;

    public async Task<ServiceResultBase> Handle(
        UpsertExperienceTypesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ExperienceTypes.Count == 0)
        {
            return Fail("At least one experience type must be provided.");
        }

        // --- Step 1: Derive sort orders before touching the database. ---
        var plan = request.ExperienceTypes
            .Select((dto, index) => new
            {
                Dto = dto,
                SortOrder = dto.SortOrder ?? (index + 1) * _SORT_ORDER_STEP
            })
            .ToArray();

        // --- Step 2: Request-level validation, before any database work. ---
        List<string> errors = [];

        string[] blankCodes = [.. plan
            .Where(item => string.IsNullOrWhiteSpace(item.Dto.Code))
            .Select(item => item.Dto.DisplayName)];

        if (blankCodes.Length > 0)
        {
            errors.Add($"Experience type code is required, and is missing for: {string.Join(", ", blankCodes)}");
        }

        string[] duplicateCodes = [.. plan
            .GroupBy(item => item.Dto.Code)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];

        if (duplicateCodes.Length > 0)
        {
            errors.Add($"Duplicate experience type code(s): {string.Join(", ", duplicateCodes)}");
        }

        // SortOrder is uniquely indexed across the whole catalogue - there is no grouping dimension to scope it to - so a collision inside one request
        // would only surface as a constraint violation from SaveChanges.
        int[] duplicateSortOrders = [.. plan
            .GroupBy(item => item.SortOrder)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];

        if (duplicateSortOrders.Length > 0)
        {
            errors.Add($"Duplicate experience type sort order(s): {string.Join(", ", duplicateSortOrders)}");
        }

        if (errors.Count > 0)
        {
            return Fail(errors);
        }

        // --- Step 3: Load the existing rows in one query. ---
        string[] codes = [.. plan.Select(item => item.Dto.Code)];

        Dictionary<string, ExperienceType> existing = await context.ExperienceTypes
            .Where(experienceType => codes.Contains(experienceType.Code))
            .ToDictionaryAsync(experienceType => experienceType.Code, cancellationToken);

        // --- Step 4: Upsert. ---
        foreach (var item in plan)
        {
            if (!existing.TryGetValue(item.Dto.Code, out ExperienceType? entity))
            {
                entity = new ExperienceType
                {
                    Code = item.Dto.Code,
                    DisplayName = item.Dto.DisplayName,
                    Description = item.Dto.Description,
                    IconPath = item.Dto.IconPath ?? string.Empty,
                    SortOrder = item.SortOrder
                };

                context.ExperienceTypes.Add(entity);
                existing[item.Dto.Code] = entity;
            }
            else
            {
                entity.DisplayName = item.Dto.DisplayName;
                entity.SortOrder = item.SortOrder;

                // IconPath and Description are only overwritten when supplied, to allow partial updates.
                if (item.Dto.IconPath is not null)
                {
                    entity.IconPath = item.Dto.IconPath;
                }

                if (item.Dto.Description is not null)
                {
                    entity.Description = item.Dto.Description;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Upserted {Count} experience type(s).", plan.Length);

        return SuccessNoContent();
    }
}
