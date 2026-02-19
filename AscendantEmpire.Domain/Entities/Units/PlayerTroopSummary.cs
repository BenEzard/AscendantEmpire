using AscendantEmpire.Domain.Entities.Games;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Units;

[Table("PlayerTroopSummaries", Schema = "game")]
public class PlayerTroopSummary
{
    [Key]
    public int Id { get; set; }
    
    public int GameId { get; set; }
    public int PlayerId { get; set; }

    /// <summary>
    /// Only populated for battle maps.
    /// </summary>
    public int? BattleMapId { get; set; }

    public int XCoord { get; set; }
    public int YCoord { get; set; }
    //public DomainType ActiveDomain { get; set; }

    public int? UnitId { get; set; }
    public int? ForceId { get; set; }

    public int SightRange { get; set; }
    //public int MovementTurns { get; set; }
    //public int HealthyTroopCount { get; set; }
    //public int InjuredTroopCount { get; set; }

    public int CreatedOnTurnId { get; set; }
    public int InactiveOnTurnId { get; set; }

    public GameIndex Game { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
