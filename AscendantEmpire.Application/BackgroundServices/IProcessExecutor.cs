using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.BackgroundServices;

/// <summary>
/// Strategy that executes a single background job for one <see cref="ProcessType"/>.
/// </summary>
/// <remarks>
/// Implementations own their own progress checkpoints and are responsible for writing the terminal <c>Complete</c>/<c>Failed</c> 
/// state to the supplied <see cref="BackgroundProcessing"/> record.
/// </remarks>
internal interface IProcessExecutor
{
    /// <summary>
    /// The process type this executor handles.
    /// </summary>
    /// <remarks>
    /// The dispatcher matches an incoming <see cref="BackgroundProcessing"/> record to the executor whose value equals the record's <c>ProcessType</c>.
    /// </remarks>
    ProcessType ProcessType { get; }

    /// <summary>
    /// Runs the job described by <paramref name="processing"/>.
    /// </summary>
    /// <remarks>
    /// The record is already loaded and tracked by a scoped context owned by the caller; implementations resolve any further dependencies they need from their own injected services.
    /// </remarks>
    Task ExecuteAsync(BackgroundProcessing processing, CancellationToken cancellationToken);
}
