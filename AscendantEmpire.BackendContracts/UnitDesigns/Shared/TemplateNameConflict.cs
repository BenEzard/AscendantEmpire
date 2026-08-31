namespace AscendantEmpire.BackendContracts.UnitDesigns.Shared;

/// <summary>
/// The detail accompanying a 409 from a unit-template save whose name is already taken: enough for the caller to decide between offering an overwrite and
/// simply reporting the refusal.
/// </summary>
/// <remarks>
/// <para>Travels in the error body's <c>Detail</c> field, alongside the human-readable message that explains the clash. The message is what gets shown;
/// this is what gets branched on.</para>
/// <para>Shared by the player-owned and global/common save flows, which answer the same conflict with different rules about who may resolve it.</para>
/// </remarks>
public record TemplateNameConflict
{
    /// <summary>
    /// True when re-sending the same save with the overwrite flag set would succeed, so the caller should offer a confirmation.
    /// </summary>
    /// <remarks>
    /// False means the clash is terminal and only a different name resolves it — a player naming a design after a global/common one, or after one of
    /// their own that already has Units in the field.
    /// </remarks>
    public bool CanOverwrite { get; init; }

    /// <summary>
    /// The existing design's stored display name, so the confirmation names it as it is actually spelled.
    /// </summary>
    /// <remarks>It can differ from the requested name in case and surrounding whitespace, since names are matched trimmed and case-insensitively.</remarks>
    public required string ExistingDisplayName { get; init; }

    /// <summary>
    /// How many Units are currently fielded from the existing design.
    /// </summary>
    /// <remarks>
    /// In the player flow anything above zero is why <see cref="CanOverwrite"/> is false. In the dev flow it never blocks the overwrite — it is reported
    /// so the confirmation can say what the change will affect.
    /// </remarks>
    public int InstanceCount { get; init; }
}
