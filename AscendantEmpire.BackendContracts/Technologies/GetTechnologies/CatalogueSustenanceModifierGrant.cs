using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.Technologies.GetTechnologies;

/// <summary>
/// One sustenance modifier a technology grants when it is discovered.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FlatValue"/> and <see cref="PercentValue"/> are the <b>raw stored values</b>, scaled as the modifier tables hold them - a twenty percent bonus
/// reads back as <c>200</c>. The biome read serves its base values the same way, so an editor showing both does not have to unscale one and not the other.
/// </para>
/// <para>
/// No target resolution is needed, unlike <see cref="CatalogueGrant"/>: both <see cref="Resource"/> and <see cref="BiomeTypeId"/> are enums the client already
/// knows how to label.
/// </para>
/// </remarks>
public record CatalogueSustenanceModifierGrant
{
    public required SustenanceResource Resource { get; init; }

    /// <summary>
    /// The biome whose value is modified, or null when the modifier applies to every biome.
    /// </summary>
    public BiomeType? BiomeTypeId { get; init; }

    public required int FlatValue { get; init; }

    public required int PercentValue { get; init; }
}
