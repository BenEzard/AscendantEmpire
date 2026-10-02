using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Classes;

/// <summary>
/// A biome's Regeneration Percent and four base sustenance values: what an unclaimed tile of the biome regrows against at the end of a Round.
/// </summary>
/// <remarks>
/// Each base value is stored scaled by <c>ScaledAmount.Scale</c>, or <see langword="null"/> where the biome does not have that resource. Read through
/// <see cref="Services.IBiomeRegenerationValueReader"/>, and shared by the end-of-Round regrowth and the support forecast so the two regrow a tile by the same amount.
/// </remarks>
internal sealed class BiomeRegenerationValues
{
    public BiomeType BiomeType { get; init; }

    /// <summary>
    /// The share of each base value that regrows each Round, as a fraction (0.25 is 25%). <see langword="null"/> for a biome with no sustenance, or for data saved before the
    /// biome validator required it.
    /// </summary>
    public decimal? RegenerationPercent { get; init; }

    public long? BaseForageValue { get; init; }

    public long? BaseHuntValue { get; init; }

    public long? BaseFishValue { get; init; }

    public long? BaseFodderValue { get; init; }

    /// <summary>
    /// The base value column that holds the given resource.
    /// </summary>
    public long? GetBaseValue(SustenanceResource resource)
    {
        return resource switch
        {
            SustenanceResource.Forage => BaseForageValue,
            SustenanceResource.Hunt => BaseHuntValue,
            SustenanceResource.Fish => BaseFishValue,
            SustenanceResource.Fodder => BaseFodderValue,
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, "Not a sustenance resource.")
        };
    }
}
