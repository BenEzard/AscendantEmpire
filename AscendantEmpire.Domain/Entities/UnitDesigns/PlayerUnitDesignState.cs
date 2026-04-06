using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

public class PlayerUnitDesignState
{
    public int PlayerId { get; set; }
    public int UnitDesignId { get; set; }

    public bool IsUnlocked { get; set; }
    public bool HasBeenPrototyped { get; set; }
    public bool IsObsolete { get; set; }

    public Player Player { get; set; } = null!;
    public UnitDesign UnitDesign { get; set; } = null!;
}
