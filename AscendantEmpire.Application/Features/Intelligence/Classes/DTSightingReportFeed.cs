using AscendantEmpire.FrontendContracts.Intelligence;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <inheritdoc cref="ISightingReportFeed" />
public class DTSightingReportFeed : ISightingReportFeed
{
    public required IReadOnlyCollection<ISightingReport> Reports { get; set; }
}
