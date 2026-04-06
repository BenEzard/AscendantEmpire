using AscendantEmpire.Domain.Entities.Games;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

public class UnitDesign
{
    public int Id { get; set; }

    /// <summary>
    /// Null for globally shared/common designs.
    /// Set for bespoke player-owned designs that only exist within a game.
    /// </summary>
    public int? GameId { get; set; }

    /// <summary>
    /// Null for globally shared/common designs.
    /// Set for bespoke player-owned designs.
    /// </summary>
    public int? PlayerId { get; set; }

    [MaxLength(50)]
    public required string DesignName { get; set; }

    /// <summary>
    /// True when this design is intended to be used as a common/template design.
    /// </summary>
    public bool IsTemplate { get; set; }

    /// <summary>
    /// Image to show on the Map.
    /// </summary>
    [MaxLength(500)]
    public required string MapImagePath { get; set; }

    public CompiledDesignStats CompiledStats { get; set; } = null!;
    public GameIndex? Game { get; set; } = null!;
    public Player? Player { get; set; } = null!;

    public ICollection<PlayerUnitDesignState> PlayerStates { get; set; } = new List<PlayerUnitDesignState>();
}
