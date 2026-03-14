using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

public class CompiledDesignStats
{
    public int UnitDesignId { get; set; }
    public int SightRange { get; set; }

    [MaxLength(500)]
    public required string MapImagePath { get; set; }
}

