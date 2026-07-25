using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Maps.Biomes.Handlers;
using AscendantEmpire.Application.Features.Maps.Geographies.Handlers;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using MediatR;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Executors;

/// <summary>
/// Executes <see cref="ProcessType.SeedDatabase"/> jobs — populates the database with its initial
/// reference/catalogue values from the static seed-data classes.
/// </summary>
/// <remarks>
/// <para>
/// Each target's values are pushed through its existing idempotent upsert handler, so re-running the seed
/// overwrites existing rows to match the code values and inserts any that are missing. Validation and the
/// biome resource-value scaling therefore stay in one place — this executor only orchestrates.
/// </para>
/// <para>
/// Steps run in a deliberate order (currently order-independent, since the three targets have no
/// inter-dependencies): add a new step by appending another <c>RunStepAsync</c> call in the desired place.
/// </para>
/// </remarks>
internal sealed class DatabaseSeedExecutor(
    AscendantEmpireContext context,
    IMediator mediator,
    ILogger<DatabaseSeedExecutor> logger
) : IProcessExecutor
{
    public ProcessType ProcessType => ProcessType.SeedDatabase;

    public async Task ExecuteAsync(Processing processing, CancellationToken cancellationToken)
    {
        int processingId = processing.ProcessingId;
        logger.LogInformation("Starting database seed for ProcessingId {ProcessingId}.", processingId);

        // Mark as in-progress so the UI can see it has started.
        processing.Status = ProcessingStatus.InProgress;
        processing.StartedAt = DateTime.UtcNow;
        processing.ProgressPercent = 5;
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            // --- Tag types ----------------------------------------------------------------
            await RunStepAsync("tag types", new UpsertTagTypesRequest(TagTypeSeedData.Build()), cancellationToken);
            await UpdateProgressAsync(processing, 40, cancellationToken);

            // --- Geographies --------------------------------------------------------------
            await RunStepAsync("geographies", new UpsertCampaignMapGeographiesRequest(GeographySeedData.Build()), cancellationToken);
            await UpdateProgressAsync(processing, 70, cancellationToken);

            // --- Biomes -------------------------------------------------------------------
            await RunStepAsync("biomes", new UpsertCampaignMapBiomesRequest(BiomeSeedData.Build()), cancellationToken);
            await UpdateProgressAsync(processing, 95, cancellationToken);

            // Mark complete. ResultReference carries a short human-readable summary for the toast.
            processing.Status = ProcessingStatus.Complete;
            processing.ProgressPercent = 100;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ResultReference = BuildSummary();
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Database seed complete for ProcessingId {ProcessingId}. {Summary}", processingId, processing.ResultReference);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database seed failed for ProcessingId {ProcessingId}.", processingId);

            processing.Status = ProcessingStatus.Failed;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ErrorMessage = ex.Message;

            // Use a non-cancellable token so the failure is persisted even during shutdown.
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Sends one seed step's upsert request and throws when it fails, so the outer catch records the failure.
    /// </summary>
    private async Task RunStepAsync(string stepName, IRequest<ServiceResultBase> request, CancellationToken cancellationToken)
    {
        ServiceResultBase result = await mediator.Send(request, cancellationToken);

        if (result.HasSucceeded == false)
        {
            throw new InvalidOperationException($"Seeding {stepName} failed: {string.Join("; ", result.Errors)}");
        }

        logger.LogInformation("Seeded {StepName}.", stepName);
    }

    /// <summary>
    /// Builds a short summary of what the seed applied, for display in the completion toast.
    /// </summary>
    private static string BuildSummary()
    {
        return $"Seeded {TagTypeSeedData.Build().Count} tag type(s), " +
            $"{GeographySeedData.Build().Count} geography(ies), " +
            $"{BiomeSeedData.Build().Count} biome(s).";
    }

    /// <summary>
    /// Persists a progress percentage update to the Processing record.
    /// </summary>
    private async Task UpdateProgressAsync(Processing processing, byte percent, CancellationToken cancellationToken)
    {
        processing.ProgressPercent = percent;
        await context.SaveChangesAsync(cancellationToken);
    }
}
