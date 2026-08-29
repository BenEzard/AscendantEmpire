namespace AscendantEmpire.BackendContracts.UnitDesigns.SaveUnitTemplate;

/// <summary>
/// The request body for creating (POST) or updating (PUT) a custom (player-owned) unit template.
/// </summary>
public record SaveUnitTemplateDto
{
    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional free-text description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Image to show on the Map.
    /// </summary>
    public required string MapImagePath { get; init; }

    /// <summary>
    /// Code of the chosen base Body component.
    /// </summary>
    public required string BaseCode { get; init; }

    /// <summary>
    /// The component fitted into each filled slot, keyed by the base body's slot code.
    /// </summary>
    /// <remarks>
    /// Only filled slots appear — an absent slot code means that slot is left empty. Keying by slot code
    /// rather than by position means adding, removing or reordering the base body's slots cannot silently
    /// re-map which component sits where.
    /// </remarks>
    public required IReadOnlyDictionary<string, string> SlotComponentCodes { get; init; }

    /// <summary>
    /// Set to confirm that saving over an existing design of the caller's with this name is intended.
    /// </summary>
    /// <remarks>
    /// <para>Read only by the create endpoint, which answers 409 with a <c>TemplateNameConflict</c> when the name is taken and this is false, and performs
    /// the overwrite when it is true. Ignored by the update endpoint, which is already addressing one specific design by id.</para>
    /// <para>It confirms an intent, it does not grant a permission: a clash the caller could never resolve — a global/common design's name, or one of
    /// their own with troops already in the field — is still refused with this set.</para>
    /// </remarks>
    public bool OverwriteExisting { get; init; }
}
