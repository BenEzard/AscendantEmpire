using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapIndex;

namespace AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapIndex;

public class UpdateCampaignMapIndexRequestDto : IUpdateCampaignMapIndexRequestDto
{
    /// <inheritdoc />
    public required string MapName { get; set; }

    /// <inheritdoc />
    public required string Description { get; set; }

    /// <inheritdoc />
    public required int Width { get; set; }

    /// <inheritdoc />
    public required int Height { get; set; }

    /// <inheritdoc />
    public required MapCreationEngine CreationEngine { get; set; }

    /// <inheritdoc />
    public required Dictionary<string, string> Parameters { get; set; }
}
