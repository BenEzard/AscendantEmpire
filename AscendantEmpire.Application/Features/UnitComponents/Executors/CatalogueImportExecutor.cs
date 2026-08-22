using System.IO.Compression;
using System.Text.Json;

using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.CatalogueTransfer;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using MediatR;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Executors;

/// <summary>
/// Executes <see cref="ProcessType.ImportCatalog"/> jobs — the inverse of <see cref="CatalogueExportExecutor"/>.
/// </summary>
/// <remarks>
/// Reads the staged export zip and replays each JSON file through the existing upsert handlers:
/// <c>components.json</c> via <see cref="BulkUpsertUnitComponentsRequest"/> and <c>templates.json</c> via
/// <see cref="BulkUpsertGlobalUnitTemplatesRequest"/>. Components are imported first because templates
/// reference component codes. Missing entities are created and changed values updated; the staged upload is
/// deleted when the job finishes (success or failure).
/// </remarks>
internal sealed class CatalogueImportExecutor(
    AscendantEmpireContext context,
    IMediator mediator,
    ILogger<CatalogueImportExecutor> logger
) : IProcessExecutor
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        // The export writes PascalCase property names and numeric enums; be lenient on casing when reading back.
        PropertyNameCaseInsensitive = true
    };

    public ProcessType ProcessType => ProcessType.ImportCatalog;

    public async Task ExecuteAsync(Processing processing, CancellationToken cancellationToken)
    {
        int processingId = processing.ProcessingId;
        logger.LogInformation("Starting catalogue import for ProcessingId {ProcessingId}.", processingId);

        // Mark as in-progress so the UI can see it has started.
        processing.Status = ProcessingStatus.InProgress;
        processing.StartedAt = DateTime.UtcNow;
        processing.ProgressPercent = 5;
        await context.SaveChangesAsync(cancellationToken);

        string zipPath = ExtractImportZipPath(processing.MetadataJson);

        try
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);

            // --- Components first (templates reference component codes) -------------------
            BulkUpsertUnitComponentsResult? componentResult = null;
            BulkUpsertUnitComponentsDto? componentsDocument = ReadEntry<BulkUpsertUnitComponentsDto>(archive, "components.json");

            if (componentsDocument is not null)
            {
                ServiceResult<BulkUpsertUnitComponentsResult> result =
                    await mediator.Send(new BulkUpsertUnitComponentsRequest(componentsDocument), cancellationToken);

                if (!result.HasSucceeded)
                {
                    throw new InvalidOperationException($"Component import failed: {string.Join("; ", result.Errors)}");
                }

                componentResult = result.SuccessValue;
            }

            await UpdateProgressAsync(processing, 55, cancellationToken);

            // --- Templates ----------------------------------------------------------------
            BulkUpsertGlobalUnitTemplatesResult? templateResult = null;
            CatalogueTemplatesDocument? templatesDocument = ReadEntry<CatalogueTemplatesDocument>(archive, "templates.json");

            if (templatesDocument is not null)
            {
                ServiceResult<BulkUpsertGlobalUnitTemplatesResult> result =
                    await mediator.Send(new BulkUpsertGlobalUnitTemplatesRequest(templatesDocument), cancellationToken);

                if (!result.HasSucceeded)
                {
                    throw new InvalidOperationException($"Template import failed: {string.Join("; ", result.Errors)}");
                }

                templateResult = result.SuccessValue;
            }

            await UpdateProgressAsync(processing, 95, cancellationToken);

            // Mark complete. ResultReference carries a short human-readable summary for the toast.
            processing.Status = ProcessingStatus.Complete;
            processing.ProgressPercent = 100;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ResultReference = BuildSummary(componentResult, templateResult);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Catalogue import complete for ProcessingId {ProcessingId}. {Summary}", processingId, processing.ResultReference);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Catalogue import failed for ProcessingId {ProcessingId}.", processingId);

            processing.Status = ProcessingStatus.Failed;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ErrorMessage = ex.Message;

            // Use a non-cancellable token so the failure is persisted even during shutdown.
            await context.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            // The staged upload is no longer needed once the job has finished (success or failure).
            TryDeleteStagedZip(zipPath);
        }
    }

    /// <summary>
    /// Deserialises a single named JSON entry from the archive, or returns null when the entry is absent
    /// (each file is optional — an export may contain only components or only templates).
    /// </summary>
    private static T? ReadEntry<T>(ZipArchive archive, string entryName) where T : class
    {
        ZipArchiveEntry? entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return null;
        }

        using Stream entryStream = entry.Open();
        T? document = JsonSerializer.Deserialize<T>(entryStream, _jsonOptions);

        if (document is null)
        {
            throw new InvalidOperationException($"The import zip's '{entryName}' could not be parsed.");
        }

        return document;
    }

    /// <summary>
    /// Builds a short summary of what the import did, for display in the completion toast.
    /// </summary>
    private static string BuildSummary(BulkUpsertUnitComponentsResult? components, BulkUpsertGlobalUnitTemplatesResult? templates)
    {
        List<string> parts = [];

        if (components is not null)
        {
            parts.Add($"Components: {components.AddedCount} added, {components.UpdatedCount} updated");
        }

        if (templates is not null)
        {
            parts.Add($"Templates: {templates.AddedCount} added, {templates.UpdatedCount} updated");
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "Nothing to import.";
    }

    /// <summary>
    /// Deletes the staged upload, swallowing and logging any failure (cleanup must never fail the job).
    /// </summary>
    private void TryDeleteStagedZip(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath))
        {
            return;
        }

        try
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete staged import zip {ZipPath}.", zipPath);
        }
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
    /// Reads the <c>importZipPath</c> value from the Processing record's MetadataJson.
    /// </summary>
    private static string ExtractImportZipPath(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            throw new InvalidOperationException("Processing record is missing MetadataJson.");
        }

        using JsonDocument doc = JsonDocument.Parse(metadataJson);
        if (!doc.RootElement.TryGetProperty("importZipPath", out JsonElement prop) ||
            prop.GetString() is not { Length: > 0 } path)
        {
            throw new InvalidOperationException("MetadataJson does not contain a valid 'importZipPath' property.");
        }

        return path;
    }
}
