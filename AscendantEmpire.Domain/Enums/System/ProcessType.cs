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
    RenderCampaignMapPreview = 3,

    /// <summary>
    /// Import a gallery map from a previously exported zip file.
    /// </summary>
    ImportGalleryMap = 4,

    /// <summary>
    /// Export selected parts of the unit-design catalogue (components and/or templates) to a zip file.
    /// </summary>
    ExportCatalogue = 5,

    /// <summary>
    /// Import a unit-design catalogue from a previously exported zip file.
    /// </summary>
    ImportCatalogue = 6,

    /// <summary>
    /// Seed the database with its initial reference/catalogue values (biomes, geographies, tag types, etc.).
    /// </summary>
    SeedDatabase = 7
}
