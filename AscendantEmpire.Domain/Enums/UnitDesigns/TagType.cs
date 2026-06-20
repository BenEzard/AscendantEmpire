namespace AscendantEmpire.Domain.Enums.UnitDesigns;

/// <summary>
/// What a <c>Tag</c> represents, so structural classifications are kept distinct from bonus tags.
/// </summary>
/// <remarks>
/// <para>Stored in the database as an <c>int</c>.</para>
/// <para><see cref="Classification"/> tags describe what a component <em>is</em> (its family/role,
/// e.g. "sword") and are intended for slot acceptance rules and UI grouping. <see cref="Buff"/> and
/// <see cref="Perk"/> tags describe bonuses a component <em>grants</em>.</para>
/// </remarks>
public enum TagType
{
    Classification,
    Buff,
    Perk,
}
