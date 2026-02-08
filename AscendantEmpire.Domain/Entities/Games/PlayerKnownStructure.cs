using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Games;

public class PlayerKnownStructure
{
    public int Id { get; set; }

    public int CampaignMapId { get; set; }

    public int GameId { get; set; }
    public int PlayerId { get; set; }

    /// <summary>
    /// Link to the source-of-truth for StructureInstances. 
    /// </summary>
    /// <remarks>This is the "world view" of structures, which may be different than what the player currently knows.</remarks>
    public int StructureInstanceId { get; set; }
    public int LastSeenOnTurnId { get; set; }

    // Snapshot of what the player currently knows
    public double ObservedCompletionPercent { get; set; }

    [ForeignKey("GameId")]
    public GameIndex Game { get; set; } = null!;

    [ForeignKey("PlayerId")]
    public Player Player { get; set; } = null!;

    [ForeignKey("StructureInstanceId")]
    public StructureInstance StructureInstance { get; set; } = null!;

    [ForeignKey("CampaignMapId")]
    public CampaignMap CampaignMap { get; set; } = null!;
}
