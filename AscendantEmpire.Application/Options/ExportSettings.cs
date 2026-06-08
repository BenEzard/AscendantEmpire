namespace AscendantEmpire.Application.Options;

/// <summary>
/// Configuration for map/data export operations.
/// Bound from the <c>ExportSettings</c> section of appsettings.
/// </summary>
public class ExportSettings
{
    /// <summary>
    /// Directory where completed export zip files are written while awaiting download.
    /// The directory is created automatically on first use if it does not exist.
    /// </summary>
    public string TempDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "AscendantEmpire", "Exports");
}
