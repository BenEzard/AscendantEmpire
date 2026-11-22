using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Services;

/// <summary>
/// The Asset each of a biome's material values is, so what a Settlement takes off the land can be named and banked.
/// </summary>
/// <remarks>
/// <para>
/// Held in code rather than on the Biome, for the same reason as <c>SustenanceResourceAssets</c>: the mapping is the same on every tile in every Game, and
/// what differs by tile is how much there is.
/// </para>
/// <para>
/// <b>Each one is the generic Asset of its kind</b> — the same three a Game's starting stock is denominated in, see <c>GameSetupService</c>. A biome states
/// how much wood a tile holds and nothing states which species it is, so naming oak here would invent a fact the catalogue does not carry. Giving a biome a
/// species of its own is a catalogue decision, and this mapping is what it would replace.
/// </para>
/// </remarks>
internal static class MaterialResourceAssets
{
    /// <summary>
    /// The Asset code each resource is identified as.
    /// </summary>
    public static readonly IReadOnlyDictionary<MaterialResource, string> CodesByResource = new Dictionary<MaterialResource, string>
    {
        [MaterialResource.Wood] = "ConstructionMaterials.Wood.Logs.GenericLog",
        [MaterialResource.Stone] = "ConstructionMaterials.Stone.RawRock.GenericRock",
        [MaterialResource.Bone] = "RawMaterials.RawMaterials.Bone"
    };
}
