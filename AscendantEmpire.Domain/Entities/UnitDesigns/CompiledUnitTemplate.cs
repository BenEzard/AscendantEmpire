using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A denormalized snapshot of a finalized <see cref="UnitTemplate"/>: its aggregated stats and resolved
/// component list, written once at finalization so reads are cheap.
/// </summary>
/// <remarks>
/// <para>One-to-one with <see cref="UnitTemplate"/> (shares its primary key).</para>
/// <para>Crucially this is read <b>without</b> the per-player unlock filter, so it can render finalized
/// designs — including <b>enemy</b> designs that contain components the viewer never unlocked. Stats are
/// stored as typed columns/complex types (not JSON) so designs can be compared and queried in SQL.</para>
/// </remarks>
public class CompiledUnitTemplate
{
    /// <summary>One-to-one with <see cref="UnitTemplate"/>; also the primary key.</summary>
    public int UnitTemplateId { get; set; }

    public required string DisplayName { get; set; }
    public required string MapImagePath { get; set; }

    /// <summary>The template's body component (the unit moves and is sized as its body).</summary>
    public int BodyComponentId { get; set; }
    public DomainType MovementDomain { get; set; }

    /// <summary>Sum of every fitted component's weight (body included).</summary>
    public long TotalWeight { get; set; }

    /// <summary>Aggregated detection across the unit's components (fixed-point, Scale 1,000).</summary>
    public long TotalDetection { get; set; }

    /// <summary>Aggregated visibility across the unit's components (fixed-point, Scale 1,000).</summary>
    public long TotalVisibility { get; set; }

    /// <summary>Campaign-map movement points, copied from the body (raw fixed-point, Scale 1,000; 1,000 = 1.0).</summary>
    public long CampaignMovementPoints { get; set; }

    /// <summary>Battlefield movement points, copied from the body (raw fixed-point, Scale 1,000; 1,000 = 1.0).</summary>
    public long BattlefieldMovementPoints { get; set; }

    /// <summary>Combat power, copied from the body (raw fixed-point, Scale 1,000; 1,000 = 1.0).</summary>
    public long Power { get; set; }

    /// <summary>Hitpoints (health), copied from the body (raw fixed-point, Scale 1,000; 1,000 = 1.0).</summary>
    public long Hitpoints { get; set; }

    public int MinimumUnitSize { get; set; }
    public int MaximumUnitSize { get; set; }

    /// <summary>Aggregated offensive output: the sum of fitted weapons' attack plus the body's crush.</summary>
    public DamageProfile Attack { get; set; } = new();

    /// <summary>Aggregated defensive mitigation: the sum of fitted armour resistance.</summary>
    public DamageProfile Resistance { get; set; } = new();

    /// <summary>The turn the snapshot was compiled on.</summary>
    public int CompiledOnTurn { get; set; }

    public UnitTemplate UnitTemplate { get; set; } = null!;

    /// <summary>The resolved components (the body plus each fitted component) for rendering the design.</summary>
    public ICollection<CompiledUnitTemplateComponent> Components { get; set; } = [];

    /// <summary>The capability tags the unit has (e.g. <c>settler</c>), unioned across its components.</summary>
    public ICollection<CompiledUnitTemplateCapability> Capabilities { get; set; } = [];
}
