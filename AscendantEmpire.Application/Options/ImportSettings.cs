namespace AscendantEmpire.Application.Options;

/// <summary>
/// Configuration for map/data import operations.
/// Bound from the <c>ImportSettings</c> section of appsettings.
/// </summary>
public class ImportSettings
{
    /// <summary>
    /// Directory where uploaded import zip files are staged while awaiting background processing.
    /// The directory is created automatically on first use if it does not exist.
    /// </summary>
    public string TempDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "AscendantEmpire", "Imports");
}
