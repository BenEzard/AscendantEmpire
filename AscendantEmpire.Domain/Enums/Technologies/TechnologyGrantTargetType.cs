namespace AscendantEmpire.Domain.Enums.Technologies;

/// <summary>
/// What kind of thing a Technology grant points at.
/// </summary>
/// <remarks>
/// <para>
/// Paired with a <c>TargetId</c> naming the particular row. It points at a different table for each member, so it carries no foreign key - the same trade
/// <c>ModifierSourceType</c> makes. The upsert handler validates that the id resolves for the type given, which is the only guard there is, so a target deleted
/// afterwards leaves the grant dangling.
/// </para>
/// <para>
/// Naming follows the existing generic pointer: <c>TargetType</c> and <c>TargetId</c> share a prefix, as <c>SourceType</c> and <c>SourceId</c> do on the
/// modifier tables.
/// </para>
/// </remarks>
public enum TechnologyGrantTargetType
{
    Asset = 1,
    AssetMetaCategory = 2,
    UnitComponent = 3,
    UnitTemplate = 4,
    MapResourceType = 5
}
