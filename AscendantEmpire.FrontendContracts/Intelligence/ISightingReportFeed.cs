namespace AscendantEmpire.FrontendContracts.Intelligence;

/// <summary>
/// A Player's intelligence feed: every report raised since their last Turn ended, oldest first.
/// </summary>
public interface ISightingReportFeed
{
    IReadOnlyCollection<ISightingReport> Reports { get; }
}
