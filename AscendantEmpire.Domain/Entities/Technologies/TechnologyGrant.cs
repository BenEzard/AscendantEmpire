using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// One thing a Technology unlocks or retires when it is discovered.
/// </summary>
/// <remarks>
/// <para>
/// Authored catalogue data. Discovering the Technology applies every one of its grants to that Player - writing the <c>PlayerUnlockedAsset</c>,
/// <c>PlayerUnlockedAssetMetaCategory</c>, <c>PlayerUnlockedComponent</c> and <c>PlayerUnlockedTemplate</c> rows those tables are waiting for. Composite
/// primary key on (TechnologyId, TargetType, TargetId).
/// </para>
/// <para>
/// One generic table rather than a typed one per target kind: a new tech-gated thing then costs an enum member instead of a table and an upsert path. The price
/// is that <see cref="TargetId"/> carries no foreign key, so a target deleted through the Entity Setup Tool after the grant was authored leaves the row
/// dangling. That is the same trade <c>ModifierSourceType</c> makes, and this is the first place it actually bites, since these targets do exist.
/// </para>
/// <para>
/// Modifier-granting technologies are <b>not</b> expressed here. A Technology that gives +10% forage writes a <c>PlayerSustenanceModifier</c> row with
/// <c>SourceType = Technology</c> and <c>SourceId</c> set to its own id, and losing the Technology is a delete by that pair. Which modifier a Technology grants
/// is authored in C#, not as catalogue data.
/// </para>
/// </remarks>
public class TechnologyGrant
{
    public int TechnologyId { get; set; }

    /// <summary>
    /// What kind of thing is granted, which decides the table <see cref="TargetId"/> refers to.
    /// </summary>
    public TechnologyGrantTargetType TargetType { get; set; }

    /// <summary>
    /// The particular Asset, meta-category, component, template or map resource type, according to <see cref="TargetType"/>.
    /// </summary>
    /// <remarks>
    /// Not a foreign key: it points at a different table for each target type. The upsert handler validates that it resolves, and nothing re-checks it after.
    /// </remarks>
    public int TargetId { get; set; }

    /// <summary>
    /// Whether discovering the Technology makes the target available or retires it.
    /// </summary>
    public TechnologyGrantEffect GrantEffect { get; set; }

    public Technology Technology { get; set; } = null!;
}
