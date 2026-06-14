using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Domain.Entities.BackgroundProcessing;

/// <summary>
/// Tracks the lifecycle of a background process (e.g. map export).
/// Process-type-specific context is stored in <see cref="MetadataJson"/>.
/// </summary>
public class Processing
{
    public int ProcessingId { get; set; }

    public ProcessType ProcessType { get; set; }

    public ProcessingStatus Status { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Human-readable error detail when <see cref="Status"/> is <see cref="ProcessingStatus.Failed"/>.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Filesystem path to the result file (e.g. the zip) once the process completes.
    /// </summary>
    public string? ResultReference { get; set; }

    /// <summary>
    /// Export progress as a value between 0 and 100. Updated by the background service at key steps.
    /// </summary>
    public byte ProgressPercent { get; set; }

    /// <summary>
    /// JSON bag of context specific to the process type (e.g. <c>{ "mapGalleryIndexId": 5 }</c>).
    /// </summary>
    public string? MetadataJson { get; set; }
}
