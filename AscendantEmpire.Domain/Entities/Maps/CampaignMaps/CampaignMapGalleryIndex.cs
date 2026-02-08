using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

public class CampaignMapGalleryIndex
{
    public const int MAX_MAP_NAME_LENGTH = 100;
    public const int MAX_DESCRIPTION_LENGTH = 500;

    public int Id { get; set; }

    [MaxLength(MAX_MAP_NAME_LENGTH)]
    public required string MapName { get; set; }

    [MaxLength(MAX_DESCRIPTION_LENGTH)]
    public string Description { get; set; } = string.Empty;

    public int Width { get; set; }
    public int Height { get; set; }
    public MapCreationStatus CreationStatus { get; set; }
    public MapCreationEngine CreationEngine { get; set; }
    public required string Status { get; set; }
    public string? ErrorMessage { get; set; }
    public required string PropertiesJson { get; set; }
    public int CampaignMapOffsetId { get; set; }

    /// <summary>
    /// Should the map wrap horizontally?
    /// </summary>
    /// <remarks>This is set by the map creator as a recommendation. 
    /// When a game is created, the player who creates the game can override the recommendation.</remarks>
    public bool HorizontalWrap { get; set; }

    /// <summary>
    /// Should the map wrap vertically?
    /// </summary>
    /// <remarks>This is set by the map creator as a recommendation. 
    /// When a game is created, the player who creates the game can override the recommendation.</remarks>
    public bool VerticalWrap { get; set; }
}
