namespace AscendantEmpire.Domain.Enums.System;

public enum ProcessType
{
    /// <summary>
    /// Export the gallery map to a file.
    /// </summary>
    ExportGalleryMap = 1,

    /// <summary>
    /// Generate the Campaign Map using the selected properties.
    /// </summary>
    GenerateCampaignMap = 2,

    /// <summary>
    /// Render the Campaign Map into a png file for Biome and Geography previews.
    /// </summary>
    RenderCampaignMapPreview = 3
}
