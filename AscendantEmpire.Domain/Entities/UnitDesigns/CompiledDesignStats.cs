namespace AscendantEmpire.Domain.Entities.UnitDesigns;

public class CompiledDesignStats
{
    /// <summary>
    /// One-to-one with UnitDesign.
    /// </summary>
    public int UnitDesignId { get; set; }
    public int SightRange { get; set; }

    /// <summary>
    /// The number of Movement Points (MP) this unit has for each turn.
    /// </summary>
    public decimal MovementPoints { get; set; }

    /// <summary>
    /// JSON-encoded list of the unit's attack bonuses
    /// </summary>
    /// <remarks>{
    /// "Category": "Movement",
    /// "Effect": "CapTileMovementCost",
    /// "Value": 1,
    /// "Conditions": [
    /// { "Type": "Biome", "Operator": "Equals", "Value": "Jungle" },
    /// { "Type": "Domain", "Operator": "Equals", "Value": "Land" }
    /// ]
    /// }</remarks>
    public string? ModifiersJson { get; set; }

    public UnitDesign UnitDesign { get; set; } = null!;
}

