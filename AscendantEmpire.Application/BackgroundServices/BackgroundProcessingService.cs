using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.BackgroundServices;

/// <summary>
/// Single dispatcher for all background jobs.
/// </summary>
/// <remarks>
/// Reads <c>ProcessingId</c>s from the <see cref="IBackgroundProcessingChannel"/>, loads the persisted <see cref="Processing"/> record, 
/// and routes it to the <see cref="IProcessExecutor"/> registered for its <see cref="ProcessType"/>. 
/// Each job runs in its own DI scope so executors get a fresh scoped <see cref="AscendantEmpireContext"/>.
/// </remarks>
public sealed class BackgroundProcessingService(IBackgroundProcessingChannel channel, 
    IServiceScopeFactory scopeFactory, 
    ILogger<BackgroundProcessingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("BackgroundProcessingService started.");

        await foreach (int processingId in channel.Reader.ReadAllAsync(stoppingToken))
        {
            await DispatchAsync(processingId, stoppingToken);
        }

        logger.LogInformation("BackgroundProcessingService stopped.");
    }

    private async Task DispatchAsync(int processingId, CancellationToken stoppingToken)
    {
        // Each job gets its own scope so the scoped DbContext (and any scoped dependencies the executor uses) are isolated per job.
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        AscendantEmpireContext context = scope.ServiceProvider.GetRequiredService<AscendantEmpireContext>();

        Processing? processing = await context.Processing.FindAsync([processingId], stoppingToken);

        if (processing is null)
        {
            logger.LogError("Processing record {ProcessingId} not found; skipping.", processingId);
            return;
        }

        // Find the executor that handles this process type.
        IEnumerable<IProcessExecutor> executors = scope.ServiceProvider.GetServices<IProcessExecutor>();
        IProcessExecutor? executor = executors.FirstOrDefault(e => e.ProcessType == processing.ProcessType);

        if (executor is null)
        {
            logger.LogError("No IProcessExecutor registered for ProcessType {ProcessType} (ProcessingId {ProcessingId}); skipping.", processing.ProcessType, processingId);
            return;
        }

        try
        {
            await executor.ExecuteAsync(processing, stoppingToken);
        }
        catch (Exception ex)
        {
            // The executor is expected to record its own failures, but if it throws before doing so
            // (or the throw is unexpected) we mark the record Failed here as a backstop.
            logger.LogError(ex, "Executor for {ProcessType} threw for ProcessingId {ProcessingId}.", 
                processing.ProcessType, processingId);

            processing.Status = ProcessingStatus.Failed;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ErrorMessage = ex.Message;

            try
            {
                await context.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx, "Failed to persist Failed status for ProcessingId {ProcessingId}.", processingId);
            }
        }
    }
}
