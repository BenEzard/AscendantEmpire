using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Application.Options;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AscendantEmpire.Application.BackgroundServices;

/// <summary>
/// Periodically deletes refresh-session rows whose expiry passed longer ago than the configured retention.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a job on the <see cref="BackgroundProcessingService"/> pipeline. That pipeline exists to run work a user started and report its
/// progress back to them, and it has no clock of its own — nothing drains it until something is enqueued. Housekeeping has no user to report to and
/// needs exactly the timer this service is, so giving it a <c>Processing</c> row and a <c>ProcessType</c> would be paperwork for an audience of
/// nobody.
/// </para>
/// <para>
/// One sweep runs at startup, then one per <see cref="RefreshSessionSettings.PruneIntervalHours"/>. A failed sweep is logged and the timer carries on:
/// an unprunable table is a growth problem, not a reason to take a hosted service down.
/// </para>
/// </remarks>
public sealed class RefreshSessionPruningService(
    IServiceScopeFactory scopeFactory,
    IOptions<RefreshSessionSettings> settings,
    ILogger<RefreshSessionPruningService> logger
) : BackgroundService
{
    /// <summary>The interval used when configuration asks for a nonsensical one, rather than spinning on a zero-length timer.</summary>
    private static readonly TimeSpan _FALLBACK_INTERVAL = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = ResolveInterval();

        logger.LogInformation(
            "RefreshSessionPruningService started; sweeping every {IntervalHours:F1}h, keeping expired sessions for {RetentionDays} day(s).",
            interval.TotalHours,
            settings.Value.RetentionDays);

        // Sweep once on the way in, so a server that is restarted more often than the interval still prunes.
        await PruneAsync(stoppingToken);

        using PeriodicTimer timer = new(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PruneAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown, not a fault.
        }

        logger.LogInformation("RefreshSessionPruningService stopped.");
    }

    /// <summary>
    /// Runs one sweep in its own DI scope, swallowing and logging any failure.
    /// </summary>
    private async Task PruneAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The session service is scoped, so the sweep needs a scope of its own rather than a captured dependency.
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

            IRefreshSessionService sessionService = scope.ServiceProvider.GetRequiredService<IRefreshSessionService>();

            await sessionService.PruneExpiredSessionsAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown mid-sweep. The next start will pick up whatever this one left.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Refresh-session pruning sweep failed; it will be retried on the next tick.");
        }
    }

    /// <summary>
    /// The configured sweep interval, falling back when configuration gives a value a timer cannot use.
    /// </summary>
    private TimeSpan ResolveInterval()
    {
        int intervalHours = settings.Value.PruneIntervalHours;

        if (intervalHours <= 0)
        {
            logger.LogWarning(
                "RefreshSessionSettings.PruneIntervalHours is {IntervalHours}, which is not a usable interval; falling back to {FallbackHours:F0}h.",
                intervalHours,
                _FALLBACK_INTERVAL.TotalHours);

            return _FALLBACK_INTERVAL;
        }

        return TimeSpan.FromHours(intervalHours);
    }
}
