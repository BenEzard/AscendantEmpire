using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Handlers;

public record UpsertStorageClassesRequest(IReadOnlyCollection<UpsertStorageClassDto> StorageClasses) : IRequest<ServiceResultBase>;

/// <summary>
/// Creates or updates storage classes, matching on Code.
/// </summary>
/// <remarks>
/// The catalogue has no endpoint, so the seed step is the only caller - but the work still goes through a handler, because that is what lets the seed
/// dispatch it through the mediator and fail the same way every other step does.
/// </remarks>
internal class UpsertStorageClassesHandler(
    ILogger<UpsertStorageClassesHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<UpsertStorageClassesRequest, ServiceResultBase>
{
    /// <summary>
    /// The comparer every code comparison in this handler uses, matching the collation the codes are stored and indexed under.
    /// </summary>
    private static readonly StringComparer _CODE_COMPARER = StringComparer.OrdinalIgnoreCase;

    public async Task<ServiceResultBase> Handle(
        UpsertStorageClassesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StorageClasses.Count == 0)
        {
            return Fail("At least one storage class must be provided.");
        }

        List<string> errors = Validate(request.StorageClasses);

        if (errors.Count > 0)
        {
            return Fail(errors);
        }

        // Load the whole catalogue in one query
        Dictionary<string, StorageClass> existing = await context.StorageClasses
            .ToDictionaryAsync(storageClass => storageClass.Code, _CODE_COMPARER, cancellationToken);

        // Upsert
        foreach (UpsertStorageClassDto storageClass in request.StorageClasses)
        {
            if (!existing.TryGetValue(storageClass.Code, out StorageClass? entity))
            {
                entity = new StorageClass
                {
                    Code = storageClass.Code,
                    DisplayName = storageClass.DisplayName
                };

                context.StorageClasses.Add(entity);
                existing[storageClass.Code] = entity;
            }
            else
            {
                entity.DisplayName = storageClass.DisplayName;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Upserted {Count} storage class(es).", request.StorageClasses.Count);

        return SuccessNoContent();
    }

    /// <summary>
    /// Collects everything wrong with the request itself, before any database work.
    /// </summary>
    /// <remarks>
    /// Every problem is gathered rather than returned on the first one, so a caller fixing an authored catalogue sees the whole list at once. Both
    /// uniqueness checks mirror a unique index, and catching them here turns a duplicate-key save failure into a message naming the offender.
    /// </remarks>
    private static List<string> Validate(IReadOnlyCollection<UpsertStorageClassDto> storageClasses)
    {
        List<string> errors = [];

        string[] blankCodes = [.. storageClasses
            .Where(storageClass => string.IsNullOrWhiteSpace(storageClass.Code))
            .Select(storageClass => storageClass.DisplayName)];

        if (blankCodes.Length > 0)
        {
            errors.Add($"Storage class code is required, and is missing for: {string.Join(", ", blankCodes)}");
        }

        string[] duplicateCodes = [.. storageClasses
            .GroupBy(storageClass => storageClass.Code, _CODE_COMPARER)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];

        if (duplicateCodes.Length > 0)
        {
            errors.Add($"Duplicate storage class code(s): {string.Join(", ", duplicateCodes)}");
        }

        string[] duplicateDisplayNames = [.. storageClasses
            .GroupBy(storageClass => storageClass.DisplayName, _CODE_COMPARER)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];

        if (duplicateDisplayNames.Length > 0)
        {
            errors.Add($"Duplicate storage class display name(s): {string.Join(", ", duplicateDisplayNames)}");
        }

        return errors;
    }
}
