using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Bulk-upserts <c>unitdesigns.Tag</c> rows on their own, without any components alongside them.
/// </summary>
/// <remarks>
/// <para>
/// The component bulk-upsert deliberately refuses an empty component list — it is the catalogue-transfer path, and a transfer carrying no components is a mistake. 
/// That left no idempotent way to refresh only the tag catalogue, which the selective database seed needs so <c>SeedTarget.Tags</c> can be picked without dragging 
/// the whole component catalogue in behind it.
/// </para>
/// <para>
/// Matched by <see cref="Tag.Code"/> through the shared <see cref="TagCatalogueUpserter"/>, so this and the component path create, re-classify and validate
/// tags identically. Nothing absent from the request is deleted: a tag added by hand survives a seed that doesn't mention it.
/// </para>
/// <para>
/// Not exposed as an API endpoint. It is dispatched through the mediator by the seed's tag step; single-tag edits from the Entity Setup Tool go through the
/// create/update handlers instead.
/// </para>
/// </remarks>
/// <param name="Tags">The tag definitions to apply. Must not be empty.</param>
public record BulkUpsertTagsRequest(IReadOnlyCollection<TagDto> Tags) : IRequest<ServiceResult<int>>;

internal class BulkUpsertTagsHandler(
    ILogger<BulkUpsertTagsHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<BulkUpsertTagsRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(BulkUpsertTagsRequest request, CancellationToken cancelToken)
    {
        try
        {
            IReadOnlyCollection<TagDto> tags = request.Tags;

            // Collect every problem so the caller gets a complete picture rather than one error at a time.
            List<string> errors = ValidateRequest(tags);

            if (errors.Count > 0)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(BulkUpsertTagsRequest), string.Join("; ", errors));
                return ServiceResult<int>.Fail(errors);
            }

            // Bulk-load the tags that already exist so they can be updated in place rather than duplicated.
            string[] tagCodes = [.. tags.Select(tag => tag.Code)];

            Dictionary<string, Tag> tagsByCode = await UnitComponentReconciler.LoadTagsByCodeAsync(context, tagCodes, cancelToken);

            int addedCount = TagCatalogueUpserter.Upsert(context, tags, tagsByCode);

            await context.SaveChangesAsync(cancelToken);

            int updatedCount = tags.Count - addedCount;

            logger.LogInformation("Bulk upserted tags: {AddedCount} added, {UpdatedCount} updated.", addedCount, updatedCount);

            // 201 when the request was purely new rows; 200 when it touched (updated) existing ones.
            return updatedCount > 0
                ? ServiceResult<int>.Succeed(tags.Count)
                : ServiceResult<int>.Created(tags.Count);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(BulkUpsertTagsHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<int>.InternalServerError(ex, error);
        }
    }

    /// <summary>
    /// Validates the shape of the request: a non-empty payload, unique codes, and each tag's own fields. Returns an empty list when the request is valid.
    /// </summary>
    private static List<string> ValidateRequest(IReadOnlyCollection<TagDto> tags)
    {
        List<string> errors = [];

        if (tags.Count == 0)
        {
            errors.Add("At least one tag must be provided.");
            return errors;
        }

        string[] duplicateCodes = TagCatalogueUpserter.FindDuplicateCodes(tags);

        if (duplicateCodes.Length > 0)
        {
            errors.Add($"Duplicate tag code(s) in the payload: {string.Join(", ", duplicateCodes)}");
        }

        foreach (TagDto tag in tags)
        {
            TagCatalogueUpserter.Validate(tag, errors);
        }

        return errors;
    }
}
