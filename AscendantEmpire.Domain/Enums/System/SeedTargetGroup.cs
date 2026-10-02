namespace AscendantEmpire.Domain.Enums.System;

/// <summary>
/// The heading a <see cref="SeedTarget"/> is listed under in the seed selection dialog.
/// </summary>
/// <remarks>
/// Presentation only — it groups the checkbox rows and breaks ties when the resolver orders two otherwise independent steps. It carries no dependency
/// meaning: a target in one group may perfectly well depend on a target in another.
/// </remarks>
public enum SeedTargetGroup
{
    /// <summary>
    /// The campaign map's reference data — geographies, biomes and map resource categories.
    /// </summary>
    Map = 1,

    /// <summary>
    /// The unit-design catalogue — tag types, tags, components and the starter templates.
    /// </summary>
    UnitDesign = 2,

    /// <summary>
    /// The asset catalogue — the meta-categories, categories, groups and Assets themselves.
    /// </summary>
    Assets = 3
}
