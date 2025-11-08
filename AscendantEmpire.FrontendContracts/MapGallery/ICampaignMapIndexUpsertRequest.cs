using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.MapGallery;

public interface ICampaignMapIndexUpsertRequest
{
    /// <summary>
    /// Identifier for the Campaign Map Index.
    /// </summary>
    /// <remarks>Null for Add, value for Update</remarks>
    int? Id { get; }
    string MapName { get; }
    string Description { get; }
    int Width { get; }
    int Height { get; }
    MapCreationEngine CreationEngine { get; }
}
