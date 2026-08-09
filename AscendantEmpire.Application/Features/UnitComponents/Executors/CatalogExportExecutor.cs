using System.IO.Compression;
using System.Text.Json;

using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.Application.Options;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.CatalogTransfer;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AscendantEmpire.Application.Features.UnitComponents.Executors;

/// <summary>
/// Executes <see cref="ProcessType.ExportCatalog"/> jobs.
/// </summary>
/// <remarks>
/// Builds a zipped export of the unit-design catalogue — <c>components.json</c> (a
/// <see cref="BulkUpsertUnitComponentsDto"/>) and/or <c>templates.json</c> (a
/// <see cref="CatalogTemplatesDocument"/>) — writes it to the configured temp directory, and records the
/// file path on the <see cref="Processing"/> record so the API can stream it back. Only catalogue data is
/// exported; game/player-owned templates are excluded. Ticking templates always pulls in the components
/// they need so the zip is self-contained.
/// </remarks>
internal sealed class CatalogExportExecutor(
    AscendantEmpireContext context,
    IOptions<ExportSettings> exportOptions,
    ILogger<CatalogExportExecutor> logger
) : IProcessExecutor
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public ProcessType ProcessType => ProcessType.ExportCatalog;

    public async Task ExecuteAsync(Processing processing, CancellationToken cancellationToken)
    {
        int processingId = processing.ProcessingId;
        logger.LogInformation("Starting catalogue export for ProcessingId {ProcessingId}.", processingId);

        // Mark as in-progress so the UI can see it has started.
        processing.Status = ProcessingStatus.InProgress;
        processing.StartedAt = DateTime.UtcNow;
        processing.ProgressPercent = 5;
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            (bool exportComponents, bool exportTemplates) = ExtractSelection(processing.MetadataJson);

            // --- Templates (and the component codes they need) ----------------------------
            // Load catalogue templates first so, even when "components" is off, we still know which
            // components must ride along for the export to be self-contained.
            List<UnitTemplate> templates = exportTemplates
                ? await LoadCatalogueTemplatesAsync(cancellationToken)
                : [];

            HashSet<string> neededComponentCodes = CollectNeededComponentCodes(templates);

            await UpdateProgressAsync(processing, 25, cancellationToken);

            // --- Components ---------------------------------------------------------------
            // Whole catalogue when "components" is ticked; otherwise just the codes the templates need.
            List<UnitComponent> components = await LoadComponentsAsync(exportComponents, neededComponentCodes, cancellationToken);

            await UpdateProgressAsync(processing, 55, cancellationToken);

            BulkUpsertUnitComponentsDto? componentsDocument = components.Count > 0
                ? await BuildComponentsDocumentAsync(components, cancellationToken)
                : null;

            await UpdateProgressAsync(processing, 75, cancellationToken);

            CatalogTemplatesDocument? templatesDocument = templates.Count > 0
                ? await BuildTemplatesDocumentAsync(templates, cancellationToken)
                : null;

            await UpdateProgressAsync(processing, 90, cancellationToken);

            // --- Zip ----------------------------------------------------------------------
            string zipPath = BuildZipPath(processingId);
            await WriteZipAsync(componentsDocument, templatesDocument, zipPath, cancellationToken);

            await UpdateProgressAsync(processing, 95, cancellationToken);

            processing.Status = ProcessingStatus.Complete;
            processing.ProgressPercent = 100;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ResultReference = zipPath;
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Catalogue export complete for ProcessingId {ProcessingId}. File: {ZipPath}", processingId, zipPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Catalogue export failed for ProcessingId {ProcessingId}.", processingId);

            processing.Status = ProcessingStatus.Failed;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ErrorMessage = ex.Message;

            // Use a non-cancellable token so the failure is persisted even during shutdown.
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Loads every catalogue (global/common) template with the graph needed to rebuild its slot-code map:
    /// its body component's slots, and each fitted component along with the slot it occupies.
    /// </summary>
    private async Task<List<UnitTemplate>> LoadCatalogueTemplatesAsync(CancellationToken cancellationToken)
    {
        return await context.UnitTemplates
            .AsNoTracking()
            .Where(t => t.Code != null && t.GameId == null && t.PlayerId == null)
            .Include(t => t.BodyComponent).ThenInclude(b => b.Body!).ThenInclude(bd => bd.Slots)
            .Include(t => t.Components).ThenInclude(c => c.UnitComponent)
            .Include(t => t.Components).ThenInclude(c => c.UnitComponentSlot)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Collects the codes of every component a set of templates references (its body plus each fitted
    /// component), so those components ride along even when the whole catalogue is not being exported.
    /// </summary>
    private static HashSet<string> CollectNeededComponentCodes(IEnumerable<UnitTemplate> templates)
    {
        HashSet<string> codes = [];

        foreach (UnitTemplate template in templates)
        {
            codes.Add(template.BodyComponent.Code);

            foreach (UnitTemplateComponent fitted in template.Components)
            {
                codes.Add(fitted.UnitComponent.Code);
            }
        }

        return codes;
    }

    /// <summary>
    /// Loads the components to export — the whole catalogue when <paramref name="exportAll"/> is set,
    /// otherwise just <paramref name="neededCodes"/> — each with its full type-specific graph.
    /// </summary>
    private async Task<List<UnitComponent>> LoadComponentsAsync(bool exportAll, HashSet<string> neededCodes, CancellationToken cancellationToken)
    {
        // Nothing to load when templates reference nothing and the whole catalogue isn't requested.
        if (!exportAll && neededCodes.Count == 0)
        {
            return [];
        }

        IQueryable<UnitComponent> query = context.UnitComponents
            .AsNoTracking()
            .Include(c => c.Body!).ThenInclude(b => b.Slots).ThenInclude(s => s.Rules)
            .Include(c => c.Weapon!).ThenInclude(w => w.DomainModifiers)
            .Include(c => c.Armour)
            .Include(c => c.Tags).ThenInclude(t => t.Tag);

        if (!exportAll)
        {
            string[] codes = [.. neededCodes];
            query = query.Where(c => codes.Contains(c.Code));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Builds the <c>components.json</c> payload from the loaded components: the components themselves, the
    /// tags they reference, and the supersession links whose both ends are in the exported set.
    /// </summary>
    private async Task<BulkUpsertUnitComponentsDto> BuildComponentsDocumentAsync(List<UnitComponent> components, CancellationToken cancellationToken)
    {
        List<UnitComponentDto> componentDtos = [.. components
            .OrderBy(c => c.Code)
            .Select(CatalogExportProjections.ToUnitComponentDto)];

        // Distinct tags referenced by the exported components (keyed by code).
        List<TagDto> tagDtos = [.. components
            .SelectMany(c => c.Tags.Select(t => t.Tag))
            .DistinctBy(t => t.Code)
            .OrderBy(t => t.Code)
            .Select(CatalogExportProjections.ToTagDto)];

        // Only keep supersession links whose predecessor AND successor are both exported, so the import
        // never references a component that isn't in the zip.
        List<int> exportedIds = [.. components.Select(c => c.Id)];

        List<ComponentSupersession> links = await context.ComponentSupersessions
            .AsNoTracking()
            .Include(s => s.Predecessor)
            .Include(s => s.Successor)
            .Where(s => exportedIds.Contains(s.PredecessorComponentId) && exportedIds.Contains(s.SuccessorComponentId))
            .ToListAsync(cancellationToken);

        List<ComponentSupersessionDto> supersessionDtos = [.. links
            .OrderBy(s => s.Predecessor.Code)
            .ThenBy(s => s.Successor.Code)
            .Select(s => new ComponentSupersessionDto
            {
                PredecessorCode = s.Predecessor.Code,
                SuccessorCode = s.Successor.Code,
                IsRecommended = s.IsRecommended,
            })];

        return new BulkUpsertUnitComponentsDto
        {
            Tags = tagDtos,
            Components = componentDtos,
            ComponentSupersessions = supersessionDtos,
        };
    }

    /// <summary>
    /// Builds the <c>templates.json</c> payload: each catalogue template (with its positional slot codes
    /// rebuilt) plus the template supersession links whose both ends are exported.
    /// </summary>
    private async Task<CatalogTemplatesDocument> BuildTemplatesDocumentAsync(List<UnitTemplate> templates, CancellationToken cancellationToken)
    {
        List<CatalogTemplateDto> templateDtos = [.. templates
            .OrderBy(t => t.Code)
            .Select(BuildTemplateDto)];

        List<int> templateIds = [.. templates.Select(t => t.Id)];

        List<TemplateSupersession> links = await context.TemplateSupersessions
            .AsNoTracking()
            .Include(s => s.Predecessor)
            .Include(s => s.Successor)
            .Where(s => templateIds.Contains(s.PredecessorTemplateId) && templateIds.Contains(s.SuccessorTemplateId))
            .ToListAsync(cancellationToken);

        List<CatalogTemplateSupersessionDto> supersessionDtos = [.. links
            .OrderBy(s => s.Predecessor.Code)
            .ThenBy(s => s.Successor.Code)
            .Select(s => new CatalogTemplateSupersessionDto
            {
                // Both ends are catalogue templates loaded above, so their Code is non-null.
                PredecessorCode = s.Predecessor.Code!,
                SuccessorCode = s.Successor.Code!,
                IsRecommended = s.IsRecommended,
            })];

        return new CatalogTemplatesDocument
        {
            Templates = templateDtos,
            TemplateSupersessions = supersessionDtos,
        };
    }

    /// <summary>
    /// Projects a single template onto its export DTO, reconstructing <c>SlotComponentCodes</c> from the
    /// fitted components by naming each one's slot code — the identity that survives the move to another
    /// database, unlike the slot id or its position.
    /// </summary>
    private static CatalogTemplateDto BuildTemplateDto(UnitTemplate template)
    {
        // Empty slots are simply absent from the map, so nothing has to stand in for them.
        Dictionary<string, string> slotComponentCodes = template.Components
            .ToDictionary(fitted => fitted.UnitComponentSlot.Code, fitted => fitted.UnitComponent.Code);

        return new CatalogTemplateDto
        {
            // A catalogue template always has a Code (filtered on load).
            Code = template.Code!,
            DisplayName = template.DisplayName,
            Description = template.Description,
            MapImagePath = template.MapImagePath,
            BaseCode = template.BodyComponent.Code,
            SlotComponentCodes = slotComponentCodes,
        };
    }

    /// <summary>
    /// Writes the export zip, adding whichever documents are present as separate JSON entries.
    /// </summary>
    private async Task WriteZipAsync(
        BulkUpsertUnitComponentsDto? componentsDocument,
        CatalogTemplatesDocument? templatesDocument,
        string zipPath,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream fileStream = new(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Create, leaveOpen: false);

        if (componentsDocument is not null)
        {
            await AddJsonEntryAsync(archive, "components.json", componentsDocument, cancellationToken);
        }

        if (templatesDocument is not null)
        {
            await AddJsonEntryAsync(archive, "templates.json", templatesDocument, cancellationToken);
        }
    }

    /// <summary>
    /// Serialises <paramref name="document"/> as indented JSON into a single archive entry.
    /// </summary>
    private static async Task AddJsonEntryAsync<T>(ZipArchive archive, string entryName, T document, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(document, _jsonOptions);

        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using Stream entryStream = entry.Open();
        await using StreamWriter writer = new(entryStream);
        await writer.WriteAsync(json.AsMemory(), cancellationToken);
    }

    private string BuildZipPath(int processingId)
    {
        string fileName = $"catalogue_export_{processingId}.zip";
        return Path.Combine(exportOptions.Value.TempDirectory, fileName);
    }

    /// <summary>
    /// Persists a progress percentage update to the Processing record.
    /// </summary>
    private async Task UpdateProgressAsync(Processing processing, byte percent, CancellationToken cancellationToken)
    {
        processing.ProgressPercent = percent;
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the <c>exportComponents</c> / <c>exportTemplates</c> flags from the Processing record's MetadataJson.
    /// </summary>
    private static (bool ExportComponents, bool ExportTemplates) ExtractSelection(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            throw new InvalidOperationException("Processing record is missing MetadataJson.");
        }

        using JsonDocument doc = JsonDocument.Parse(metadataJson);
        JsonElement root = doc.RootElement;

        bool exportComponents = root.TryGetProperty("exportComponents", out JsonElement componentsProp) && componentsProp.GetBoolean();
        bool exportTemplates = root.TryGetProperty("exportTemplates", out JsonElement templatesProp) && templatesProp.GetBoolean();

        if (!exportComponents && !exportTemplates)
        {
            throw new InvalidOperationException("MetadataJson selects neither components nor templates to export.");
        }

        return (exportComponents, exportTemplates);
    }
}
