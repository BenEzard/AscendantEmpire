using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Maps.Biomes.Handlers;
using AscendantEmpire.Application.Features.Maps.Geographies.Handlers;
using AscendantEmpire.Application.Features.Maps.MapResources.Handlers;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Executors;

/// <summary>
/// Executes <see cref="ProcessType.SeedDatabase"/> jobs — populates the database with its initial reference values from the static seed-data classes.
/// </summary>
/// <remarks>
/// <para>
/// Each target's values are pushed through its existing idempotent upsert handler, so re-running the seed overwrites existing rows to match the code values and
/// inserts any that are missing. Validation and the biome resource-value scaling therefore stay in one place — this executor only orchestrates.
/// </para>
/// <para>
/// Steps run in a deliberate order: the map reference data first (those four targets have no inter-dependencies), then the unit-design catalogue, where order
/// does matter — components carry the tags, and templates are assembled from components by code. Add a new step by appending another <c>RunStepAsync</c> call
/// in the desired place.
/// </para>
/// <para>
/// The unit-design steps cover everything <c>docs/design/starting-requirements.md</c> asks for, so a freshly seeded database can create a game without any
/// hand-authoring: the organisation tags, the human body and the components that carry those tags, and the <c>SETTLER</c> and <c>SCOUT</c> templates
/// <c>GameSetupService.StarterTemplateCodes</c> demands. Templates compile as they are upserted, so no recompile pass is needed afterwards.
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
            await UpdateProgressAsync(processing, 25, cancellationToken);

            // --- Geographies --------------------------------------------------------------
            await RunStepAsync("geographies", new UpsertCampaignMapGeographiesRequest(GeographySeedData.Build()), cancellationToken);
            await UpdateProgressAsync(processing, 40, cancellationToken);

            // --- Biomes -------------------------------------------------------------------
            await RunStepAsync("biomes", new UpsertCampaignMapBiomesRequest(BiomeSeedData.Build()), cancellationToken);
            await UpdateProgressAsync(processing, 55, cancellationToken);

            // --- MapResource categories ---------------------------------------------------
            // Categories only: see MapResourceCategorySeedData for why no MapResourceTypes are seeded.
            await RunStepAsync("map resource categories", new PutMapResourceCatalogueRequest(MapResourceCategorySeedData.Build()), cancellationToken);
            await UpdateProgressAsync(processing, 65, cancellationToken);

            // --- Tags and unit components -------------------------------------------------
            // The tags travel inside this payload rather than as a step of their own — the component bulk-upsert is the only idempotent path that writes them.
            // Must precede the templates, which name their base body and fitted components by code.
            await RunStepAsync("unit components", new BulkUpsertUnitComponentsRequest(UnitComponentSeedData.Build()), cancellationToken);
            await UpdateProgressAsync(processing, 85, cancellationToken);

            // --- Starter unit templates ---------------------------------------------------
            // Compiled as they are saved, so each arrives with a live snapshot and game setup finds it ready.
            await RunStepAsync("starter unit templates", new BulkUpsertGlobalUnitTemplatesRequest(StarterTemplateSeedData.Build()), cancellationToken);
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
    /// <remarks>
    /// Generic over the response because a handler is resolved against the response type the caller supplies, not the request's runtime type: a request
    /// declaring <c>ServiceResult&lt;T&gt;</c> cannot be dispatched through an <c>IRequest&lt;ServiceResultBase&gt;</c> parameter and still find its handler.
    /// Inference keeps the call sites unchanged for the steps that do declare the base type.
    /// </remarks>
    private async Task RunStepAsync<TResponse>(string stepName, IRequest<TResponse> request, CancellationToken cancellationToken)
        where TResponse : ServiceResultBase
    {
        ServiceResultBase result = await mediator.Send(request, cancellationToken);

        ThrowIfFailed(stepName, result);

        logger.LogInformation("Seeded {StepName}.", stepName);
    }

    /// <summary>
    /// Throws when a seed step's result is a failure, so the outer catch records it against the Processing row.
    /// </summary>
    private static void ThrowIfFailed(string stepName, ServiceResultBase result)
    {
        if (result.HasSucceeded == false)
        {
            throw new InvalidOperationException($"Seeding {stepName} failed: {string.Join("; ", result.Errors)}");
        }
    }

    /// <summary>
    /// Builds a short summary of what the seed applied, for display in the completion toast.
    /// </summary>
    private static string BuildSummary()
    {
        BulkUpsertUnitComponentsDto components = UnitComponentSeedData.Build();

        return $"Seeded {TagTypeSeedData.Build().Count} tag type(s), " +
            $"{GeographySeedData.Build().Count} geography(ies), " +
            $"{BiomeSeedData.Build().Count} biome(s), " +
            $"{MapResourceCategorySeedData.Build().Categories.Count} map resource category(ies), " +
            $"{components.Tags.Count} tag(s), " +
            $"{components.Components.Count} unit component(s), " +
            $"{StarterTemplateSeedData.Build().Templates.Count} starter unit template(s).";
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
