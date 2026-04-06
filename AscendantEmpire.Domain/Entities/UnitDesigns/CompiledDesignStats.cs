namespace AscendantEmpire.Domain.Entities.UnitDesigns;

public class CompiledDesignStats
{
    /// <summary>
    /// One-to-one with UnitDesign.
    /// </summary>
    public int UnitDesignId { get; set; }
    public int SightRange { get; set; }

    public UnitDesign UnitDesign { get; set; } = null!;
}

