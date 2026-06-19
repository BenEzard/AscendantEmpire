namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Join row linking a <see cref="UnitComponent"/> to a <see cref="Tag"/> (many-to-many).
/// </summary>
public class UnitComponentTag
{
    public int UnitComponentId { get; set; }
    public int TagId { get; set; }

    public UnitComponent UnitComponent { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
