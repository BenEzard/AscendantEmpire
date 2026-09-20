using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.Classes;
using AscendantEmpire.Application.Features.DatabaseSeeding.Steps;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Executors;

/// <summary>
/// Executes <see cref="ProcessType.SeedDatabase"/> jobs — populates the selected parts of the database with their initial values from the static seed-data
/// classes.
/// </summary>
/// <remarks>
/// <para>
/// Each target's values are pushed through its existing idempotent upsert handler, so re-running the seed overwrites existing rows to match the code values
/// and inserts any that are missing. Validation, and the biome resource-value scaling, therefore stay in one place — this executor only orchestrates.
/// </para>
/// <para>
/// What runs is a selection, not a fixed list: the job carries its expanded target list in <c>MetadataJson</c>, and <see cref="SeedTargetResolver"/> turns
/// that into the registered <see cref="ISeedStep"/>s in an order that never puts a step before something it depends on. Adding a new part of the seed means
/// adding a step and registering it — there is nothing to append here.
/// </para>
/// <para>
/// A job whose metadata cannot be read seeds everything. That is what a job queued before the selection existed meant, and it is the only reading that
/// cannot silently under-seed a database someone believed they had just filled.
/// </para>
/// </remarks>
internal sealed class DatabaseSeedExecutor(
    AscendantEmpireContext context,
    IMediator mediator,
    IEnumerable<ISeedStep> seedSteps,
    ILogger<DatabaseSeedExecutor> logger
) : IProcessExecutor
{
    /// <summary>Progress recorded once the job has started but before the first step has finished.</summary>
    private const byte _PROGRESS_ON_START = 5;

    /// <summary>The share of the bar the steps themselves divide up, leaving the rest for starting and finishing.</summary>
    private const int _PROGRESS_SPAN_FOR_STEPS = 90;

    public ProcessType ProcessType => ProcessType.SeedDatabase;

    public async Task ExecuteAsync(Processing processing, CancellationToken cancellationToken)
    {
        int processingId = processing.ProcessingId;

        // Mark as in-progress so the UI can see it has started.
        processing.Status = ProcessingStatus.InProgress;
        processing.StartedAt = DateTime.UtcNow;
        processing.ProgressPercent = _PROGRESS_ON_START;
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            IReadOnlyList<ISeedStep> steps = ResolveSteps(processing);

            logger.LogInformation("Starting database seed for ProcessingId {ProcessingId}. Steps: {Steps}.",
                processingId,
                string.Join(", ", steps.Select(step => step.Target)));

            // Each step's fragment of the summary, collected as it runs so the summary describes what actually happened rather than rebuilding every seed-data class a second time to count it.
            List<string> summaryFragments = [];

            for (int stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                ISeedStep step = steps[stepIndex];

                summaryFragments.Add(await step.ExecuteAsync(mediator, cancellationToken));

                logger.LogInformation("Seeded {StepName}.", step.DisplayName);

                await UpdateProgressAsync(processing, StepProgress(stepIndex, steps.Count), cancellationToken);
            }

            // Mark complete. ResultReference carries a short human-readable summary.
            processing.Status = ProcessingStatus.Complete;
            processing.ProgressPercent = 100;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ResultReference = $"Seeded {string.Join(", ", summaryFragments)}.";
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
    /// Works out which steps this job is to run, from the target list it carries.
    /// </summary>
    /// <remarks>
    /// Metadata that cannot be read falls back to every registered target — see the class remarks for why that is the safe reading rather than a failure.
    /// </remarks>
    private IReadOnlyList<ISeedStep> ResolveSteps(Processing processing)
    {
        IReadOnlyCollection<SeedTarget>? targets = DatabaseSeedMetadata.TryRead(processing.MetadataJson);

        if (targets is null)
        {
            logger.LogWarning("ProcessingId {ProcessingId} carries no readable seed target list; seeding every target.", processing.ProcessingId);

            targets = [.. seedSteps.Select(step => step.Target)];
        }

        return SeedTargetResolver.Resolve(seedSteps, targets);
    }

    /// <summary>
    /// The progress to record once the step at <paramref name="stepIndex"/> has finished.
    /// </summary>
    /// <remarks>
    /// The steps divide the bar evenly. They are not equal in cost — the components step does far more than the geographies one — but the seed is short
    /// enough that a weighting would be precision without purpose, and an even split cannot stall or run backwards when the selection changes.
    /// </remarks>
    private static byte StepProgress(int stepIndex, int stepCount)
    {
        double completedShare = (stepIndex + 1) / (double)stepCount;

        return (byte)(_PROGRESS_ON_START + (int)(completedShare * _PROGRESS_SPAN_FOR_STEPS));
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
