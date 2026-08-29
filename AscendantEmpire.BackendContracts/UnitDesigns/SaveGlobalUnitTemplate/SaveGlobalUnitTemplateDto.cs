namespace AscendantEmpire.BackendContracts.UnitDesigns.SaveGlobalUnitTemplate;

/// <summary>
/// The request body for the dev design tool's create (POST) / update (PUT) of a global/common unit
/// template in the shared catalogue.
/// </summary>
public record SaveGlobalUnitTemplateDto
{
    /// <summary>
    /// Unique, stable key for the template, e.g. "scout_walker_mk1".
    /// </summary>
    public required string Code { get; init; }

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
    /// Set to confirm that saving over the existing catalogue template with this <see cref="Code"/> is intended.
    /// </summary>
    /// <remarks>
    /// <para>Read only by the create endpoint, which answers 409 with a <c>TemplateNameConflict</c> when the code is taken and this is false, and performs
    /// the overwrite when it is true. Ignored by the update endpoint, which is already addressing one specific template by id.</para>
    /// <para>Unlike the player-facing flow, nothing here is ever unresolvable: authoring the shared catalogue is this endpoint's purpose, so troops
    /// already built from the template are reported in the conflict but never refuse the overwrite.</para>
    /// </remarks>
    public bool OverwriteExisting { get; init; }
}
