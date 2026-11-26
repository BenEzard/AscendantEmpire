using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

/// <summary>
/// The <c>technology.ExperienceType</c> codes a producing system names when it earns experience.
/// </summary>
/// <remarks>
/// <para>
/// Each producing system resolves its own concept to a code here and hands that to <c>IExperienceAllocator</c>, so nothing outside the Technology feature ever
/// holds an <c>ExperienceType</c> id. Movement knows about biomes; it does not know that a technology schema exists.
/// </para>
/// <para>
/// A code produced here must match a row in <c>ExperienceTypeSeedData</c>, and <c>ExperienceTypeCodesTests</c> holds the two together. That test is the real
/// guard: the allocator refuses a batch naming a code it cannot resolve, and the end of Turn carries on past a refusal, so a <see cref="BiomeType"/> member
/// added without a seeded row would cost every Player that Turn's whole biome experience for nothing louder than a warning.
/// </para>
/// </remarks>
public static class ExperienceTypeCodes
{
    /// <summary>
    /// Earned by uncovering terrain, and the one kind that belongs to no family.
    /// </summary>
    public const string EXPLORATION = "Exploration";

    private const string _BIOME_PREFIX = "Biome.";

    /// <summary>
    /// The code for operating in one Biome, or <see langword="null"/> where the Biome is not terrain at all.
    /// </summary>
    /// <remarks>
    /// Derived from the member name rather than listed, because <c>ExperienceTypeSeedData</c> authors one row per real member under exactly that spelling.
    /// <see cref="BiomeType.Unknown"/> and <see cref="BiomeType.Void"/> are the absence of terrain rather than a kind of it, so neither earns anything.
    /// </remarks>
    public static string? ForBiome(BiomeType biomeType)
    {
        if (biomeType is BiomeType.Unknown or BiomeType.Void)
        {
            return null;
        }

        return _BIOME_PREFIX + biomeType;
    }
}
