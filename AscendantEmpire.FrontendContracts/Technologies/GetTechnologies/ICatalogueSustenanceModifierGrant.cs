using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

/// <summary>
/// One sustenance modifier a technology grants when it is discovered.
/// </summary>
/// <remarks>
/// The two values are raw and scaled, as the modifier tables hold them - a twenty percent bonus reads back as <c>200</c>.
/// </remarks>
public interface ICatalogueSustenanceModifierGrant
{
    SustenanceResource Resource { get; }

    /// <summary>
    /// The biome whose value is modified, or null when the modifier applies to every biome.
    /// </summary>
    BiomeType? BiomeTypeId { get; }

    int FlatValue { get; }

    int PercentValue { get; }
}
