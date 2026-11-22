using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.Technologies;

/// <summary>
/// One sustenance modifier a Technology grants when it is discovered, as a designer authors it.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="FlatValue"/> and <see cref="PercentValue"/> are whole numbers, not scaled ones.</b> <see cref="PercentValue"/> of <c>20</c> means twenty percent and
/// is stored as <c>200</c>; <see cref="FlatValue"/> of <c>5</c> means five units of the resource and is stored as <c>5000</c>. The handler scales both, following the
/// biome upsert, so nothing outside it ever writes a raw value.
/// </para>
/// <para>
/// At least one of the two must be non-zero, which mirrors <c>CK_TechnologySustenanceModifierGrant_HasValue</c>. Either may be negative, for a Technology that makes a
/// resource harder to gather.
/// </para>
/// <para>
/// A named <see cref="BiomeTypeId"/> must have a base value for <see cref="Resource"/>, because no modifier can raise a null base. The upsert rejects a grant that
/// would therefore do nothing, which is why the technology seed depends on the Biomes catalogue.
/// </para>
/// </remarks>
public record UpsertTechnologySustenanceModifierGrantDto
{
    /// <summary>
    /// Which sustenance value is modified.
    /// </summary>
    public required SustenanceResource Resource { get; init; }

    /// <summary>
    /// The biome whose value is modified, or null for every biome.
    /// </summary>
    public BiomeType? BiomeTypeId { get; init; }

    /// <summary>
    /// A flat amount of the resource added to the value, as a whole number. Negative for a penalty; 0 when this grant has no flat part.
    /// </summary>
    public int FlatValue { get; init; }

    /// <summary>
    /// A whole percentage of the base value added to the value. Negative for a penalty; 0 when this grant has no percentage part.
    /// </summary>
    public int PercentValue { get; init; }
}
