using AscendantEmpire.Domain.Enums.Processing;

namespace AscendantEmpire.Application.Features.SystemMonitoring.Classes;

/// <summary>
/// Represents the current state of a background process for API consumers.
/// <see cref="ResultReference"/> is intentionally excluded — file paths are internal.
/// </summary>
public class DTProcessingStatus
{
    public int ProcessingId { get; init; }
    public ProcessType ProcessType { get; init; }
    public ProcessingStatus Status { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
}
