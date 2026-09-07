using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.CatalogueTransfer;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Bulk-upserts catalogue (global/common) unit templates, matched by <c>Code</c>.
/// </summary>
/// <remarks>
/// <para>Mirrors <c>BulkUpsertUnitComponentsHandler</c> at the template level and backs catalogue import.
/// Templates whose <c>Code</c> is new are inserted; templates whose <c>Code</c> already exists are updated
/// in place, replacing their fitted components. Each template is resolved and validated through the shared
/// <see cref="UnitTemplateAssembly"/>, exactly like the create/update handlers, so the same assembly rules
/// apply. Referenced components must already exist (import upserts components before templates).</para>
/// <para>Template supersession links are reconciled per predecessor, mirroring the component behaviour;
/// links whose ends do not resolve to a known template are skipped.</para>
/// <para>Every upserted template's <c>CompiledUnitTemplate</c> snapshot is (re)compiled in the same transaction
/// (via <see cref="CompileUnitTemplateHandler"/>), so imported catalogues never carry stale or missing snapshots.
/// Global templates aren't game-scoped, so snapshots are stamped with turn <c>0</c>.</para>
/// </remarks>
public record BulkUpsertGlobalUnitTemplatesRequest(CatalogueTemplatesDocument Document) : IRequest<ServiceResult<BulkUpsertGlobalUnitTemplatesResult>>;

internal class BulkUpsertGlobalUnitTemplatesHandler(
    ILogger<BulkUpsertGlobalUnitTemplatesHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator
) : IRequestHandler<BulkUpsertGlobalUnitTemplatesRequest, ServiceResult<BulkUpsertGlobalUnitTemplatesResult>>
{
    public async Task<ServiceResult<BulkUpsertGlobalUnitTemplatesResult>> Handle(BulkUpsertGlobalUnitTemplatesRequest request, CancellationToken cancelToken)
    {
        try
        {
            CatalogueTemplatesDocument document = request.Document;

            // An empty payload is a valid no-op (e.g. a components-only export re-imported).
            if (document.Templates.Count == 0)
            {
                return ServiceResult<BulkUpsertGlobalUnitTemplatesResult>.Succeed(new BulkUpsertGlobalUnitTemplatesResult
                {
                    AddedCount = 0,
                    UpdatedCount = 0,
                    UpdatedCodes = [],
                });
            }

            // Duplicate codes within the payload would make the upsert ambiguous — reject up front.
            string[] duplicateCodes = [.. document.Templates
                .GroupBy(t => t.Code)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)];

            if (duplicateCodes.Length > 0)
            {
                return ServiceResult<BulkUpsertGlobalUnitTemplatesResult>.Fail($"Duplicate template code(s) in the payload: {string.Join(", ", duplicateCodes)}");
            }

            // Resolve + validate every template against the catalogue before writing anything, so a rejected
            // request writes nothing. Resolution reads the (already-upserted) components and builds the
            // fitted-component rows, applying the same assembly rules as the create/update handlers.
            Dictionary<string, UnitTemplateAssembly.ResolvedAssembly> resolvedByCode = [];
            List<string> errors = [];

            foreach (CatalogueTemplateDto templateDto in document.Templates)
            {
                ServiceResult<UnitTemplateAssembly.ResolvedAssembly> resolved = await UnitTemplateAssembly.ResolveAsync(
                    context, playerId: null, templateDto.DisplayName, templateDto.MapImagePath, templateDto.BaseCode, templateDto.SlotComponentCodes, cancelToken);

                if (resolved.HasSucceeded)
                {
                    resolvedByCode[templateDto.Code] = resolved.SuccessValue!;
                }
                else
                {
                    errors.AddRange(resolved.Errors.Select(e => $"Template '{templateDto.Code}': {e}"));
                }
            }

            if (errors.Count > 0)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(BulkUpsertGlobalUnitTemplatesRequest), string.Join("; ", errors));
                return ServiceResult<BulkUpsertGlobalUnitTemplatesResult>.Fail(errors);
            }

            // Load any catalogue templates that already exist for the payload's codes, with their fitted
            // components so those can be replaced on update.
            string[] codes = [.. document.Templates.Select(t => t.Code)];

            Dictionary<string, UnitTemplate> existingByCode = await context.UnitTemplates
                .Where(t => t.Code != null && t.GameId == null && t.PlayerId == null && codes.Contains(t.Code))
                .Include(t => t.Components)
                .ToDictionaryAsync(t => t.Code!, cancelToken);

            // Upsert + compile atomically: every upserted template must end up with a fresh snapshot, so a compile
            // failure part-way through must roll back the whole import. CompileUnitTemplateHandler shares this
            // request scope's context, so one transaction covers everything; disposing without Commit rolls back.
            await using var transaction = await context.Database.BeginTransactionAsync(cancelToken);

            // A code -> template map (existing + newly built) lets the supersession step wire links by
            // navigation, so EF assigns foreign keys even for brand-new templates on save.
            Dictionary<string, UnitTemplate> templatesByCode = new(existingByCode);
            List<string> addedCodes = [];
            List<string> updatedCodes = [];

            foreach (CatalogueTemplateDto templateDto in document.Templates)
            {
                UnitTemplateAssembly.ResolvedAssembly assembly = resolvedByCode[templateDto.Code];

                if (existingByCode.TryGetValue(templateDto.Code, out UnitTemplate? existing))
                {
                    UpdateTemplate(existing, templateDto, assembly);
                    updatedCodes.Add(templateDto.Code);
                }
                else
                {
                    UnitTemplate built = BuildTemplate(templateDto, assembly);
                    context.UnitTemplates.Add(built);
                    templatesByCode[templateDto.Code] = built;
                    addedCodes.Add(templateDto.Code);
                }
            }

            // Reconcile the template supersession (upgrade) links from the payload.
            if (document.TemplateSupersessions.Count > 0)
            {
                await ReconcileTemplateSupersessionsAsync(document, templatesByCode, cancelToken);
            }

            await context.SaveChangesAsync(cancelToken);

            // (Re)compile every upserted template's snapshot — templates loaded only for supersession links are
            // untouched, so their existing snapshots remain valid. Ids exist now that the save has run.
            foreach (string code in addedCodes.Concat(updatedCodes))
            {
                ServiceResult<int> compiled = await mediator.Send(
                    new CompileUnitTemplateRequest(UnitTemplateId: templatesByCode[code].Id, CompiledOnTurn: 0), cancelToken);

                if (!compiled.HasSucceeded)
                {
                    logger.LogWarning("Compilation failed for template '{Code}' during {Request}. Errors: {Errors}",
                        code, nameof(BulkUpsertGlobalUnitTemplatesRequest), string.Join("; ", compiled.Errors));
                    return new ServiceResult<BulkUpsertGlobalUnitTemplatesResult>
                    {
                        StatusCode = compiled.StatusCode,
                        Errors = [.. compiled.Errors.Select(e => $"Template '{code}': {e}")],
                    };
                }
            }

            await transaction.CommitAsync(cancelToken);

            logger.LogInformation("Bulk upserted catalogue templates: {AddedCount} added, {UpdatedCount} updated.", addedCodes.Count, updatedCodes.Count);

            return ServiceResult<BulkUpsertGlobalUnitTemplatesResult>.Succeed(new BulkUpsertGlobalUnitTemplatesResult
            {
                AddedCount = addedCodes.Count,
                UpdatedCount = updatedCodes.Count,
                UpdatedCodes = updatedCodes,
            });
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(BulkUpsertGlobalUnitTemplatesHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<BulkUpsertGlobalUnitTemplatesResult>.InternalServerError(ex, error);
        }
    }

    /// <summary>
    /// Builds a brand-new catalogue template from a DTO and its resolved assembly.
    /// </summary>
    private static UnitTemplate BuildTemplate(CatalogueTemplateDto dto, UnitTemplateAssembly.ResolvedAssembly assembly)
    {
        return new UnitTemplate
        {
            Code = dto.Code,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            GameId = null,
            PlayerId = null,
            BodyComponentId = assembly.BaseComponent.Id,
            MapImagePath = dto.MapImagePath,
            Components = assembly.FittedComponents,
        };
    }

    /// <summary>
    /// Overwrites an existing tracked catalogue template from the DTO: scalar fields, its base body and its
    /// fitted components (old rows deleted, resolved rows attached).
    /// </summary>
    private void UpdateTemplate(UnitTemplate existing, CatalogueTemplateDto dto, UnitTemplateAssembly.ResolvedAssembly assembly)
    {
        existing.DisplayName = dto.DisplayName;
        existing.Description = dto.Description;
        existing.MapImagePath = dto.MapImagePath;
        existing.BodyComponentId = assembly.BaseComponent.Id;

        // Replace the fitted-component tree wholesale (NoAction FKs => delete the old rows explicitly).
        context.UnitTemplateComponents.RemoveRange(existing.Components);
        existing.Components = assembly.FittedComponents;
    }

    /// <summary>
    /// Reconciles each mentioned predecessor's outgoing template supersession links to exactly those in the
    /// payload — updating <c>IsRecommended</c> on kept links, adding new ones and deleting the surplus.
    /// Links whose ends do not resolve to a known template are skipped.
    /// </summary>
    private async Task ReconcileTemplateSupersessionsAsync(
        CatalogueTemplatesDocument document,
        Dictionary<string, UnitTemplate> templatesByCode,
        CancellationToken cancelToken)
    {
        // Links may reference catalogue templates that already exist but aren't in this payload — load them.
        await LoadTemplatesReferencedBySupersessionsAsync(document, templatesByCode, cancelToken);

        // Only keep links whose predecessor and successor both resolve to a known template.
        List<CatalogueTemplateSupersessionDto> resolvableLinks = [.. document.TemplateSupersessions
            .Where(link => templatesByCode.ContainsKey(link.PredecessorCode) && templatesByCode.ContainsKey(link.SuccessorCode))];

        foreach (IGrouping<string, CatalogueTemplateSupersessionDto> desiredForPredecessor in resolvableLinks.GroupBy(s => s.PredecessorCode))
        {
            UnitTemplate predecessor = templatesByCode[desiredForPredecessor.Key];

            // Existing links only exist for an already-persisted predecessor (a brand-new template has none).
            List<TemplateSupersession> existingLinks = predecessor.Id == 0
                ? []
                : await context.TemplateSupersessions
                    .Where(s => s.PredecessorTemplateId == predecessor.Id)
                    .ToListAsync(cancelToken);

            Dictionary<int, TemplateSupersession> existingBySuccessorId = existingLinks.ToDictionary(s => s.SuccessorTemplateId);
            HashSet<int> keptSuccessorIds = [];

            foreach (CatalogueTemplateSupersessionDto desired in desiredForPredecessor)
            {
                UnitTemplate successor = templatesByCode[desired.SuccessorCode];

                if (successor.Id != 0 && existingBySuccessorId.TryGetValue(successor.Id, out TemplateSupersession? existingLink))
                {
                    existingLink.IsRecommended = desired.IsRecommended;
                    keptSuccessorIds.Add(successor.Id);
                }
                else
                {
                    context.TemplateSupersessions.Add(new TemplateSupersession
                    {
                        Predecessor = predecessor,
                        Successor = successor,
                        IsRecommended = desired.IsRecommended,
                    });
                }
            }

            // Drop links whose successor is no longer requested (NoAction FK => delete explicitly).
            List<TemplateSupersession> removedLinks = [.. existingLinks.Where(s => !keptSuccessorIds.Contains(s.SuccessorTemplateId))];
            context.TemplateSupersessions.RemoveRange(removedLinks);
        }
    }

    /// <summary>
    /// Loads any catalogue template referenced by a supersession link that isn't already tracked.
    /// </summary>
    private async Task LoadTemplatesReferencedBySupersessionsAsync(
        CatalogueTemplatesDocument document,
        Dictionary<string, UnitTemplate> templatesByCode,
        CancellationToken cancelToken)
    {
        string[] missingCodes = [.. document.TemplateSupersessions
            .SelectMany(s => new[] { s.PredecessorCode, s.SuccessorCode })
            .Where(code => !string.IsNullOrWhiteSpace(code) && !templatesByCode.ContainsKey(code))
            .Distinct()];

        if (missingCodes.Length == 0)
        {
            return;
        }

        List<UnitTemplate> loaded = await context.UnitTemplates
            .Where(t => t.Code != null && t.GameId == null && t.PlayerId == null && missingCodes.Contains(t.Code))
            .ToListAsync(cancelToken);

        foreach (UnitTemplate template in loaded)
        {
            templatesByCode[template.Code!] = template;
        }
    }
}
