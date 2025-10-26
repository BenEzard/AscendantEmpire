using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.GenerateCampaignMap;

namespace AscendantEmpire.BackendContracts.MapGallery.GenerateCampaignMap;

public record GenerateCampaignMapDto : IGenerateCampaignMapDto
{
    /// <inheritdoc />
    public required string MapName { get; init; }
    /// <inheritdoc />
    public string Description { get; init; }
    /// <inheritdoc />
    public required int Width { get; init; }
    /// <inheritdoc />
    public required int Height { get; init; }
    /// <inheritdoc />
    public required MapCreationEngine CreationEngine { get; init; }
    /// <inheritdoc />
    public required Dictionary<string, string>? Parameters { get; init; } = [];
}
