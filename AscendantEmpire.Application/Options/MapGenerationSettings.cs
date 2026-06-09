namespace AscendantEmpire.Application.Options;

/// <summary>
/// Configuration for map generation artefacts (preview PNGs, etc.).
/// Bound from the <c>MapGenerationSettings</c> section of appsettings.
/// </summary>
public class MapGenerationSettings
{
    /// <summary>
    /// Directory where generated map preview PNG files are written.
    /// The directory is created automatically if it does not exist.
    /// Defaults to a subfolder of the system temp directory.
    /// </summary>
    public string OutputDirectory { get; set; } =
        Path.Combine(Path.GetTempPath(), "AscendantEmpire", "MapPreviews");
}
