namespace AscendantEmpire.Application.Options;

/// <summary>
/// Configuration for the background processing pipeline.
/// </summary>
/// <remarks>
/// Bound from the <c>BackgroundProcessingSettings</c> section of appsettings.
/// </remarks>
public class BackgroundProcessingSettings
{
    /// <summary>
    /// Maximum number of jobs that may sit queued in the in-process channel before writers block.
    /// </summary>
    /// <remarks>
    /// Jobs are user-initiated and processed one at a time, so this is a generous backpressure ceiling rather than a concurrency limit.
    /// </remarks>
    public int ChannelCapacity { get; set; } = 50;
}
