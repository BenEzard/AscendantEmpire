namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// Request payload for bulk-upserting unit components into the global catalog.
/// </summary>
/// <remarks>
/// <para>The full component graph is written in a single transaction.</para>
/// <para>Components whose <c>Code</c> is new are inserted; components whose <c>Code</c> already
/// exists are updated in place (their <c>ComponentType</c> cannot change).</para>
/// </remarks>
public record BulkUpsertUnitComponentsDto
{
    /// <summary>
    /// Tag definitions referenced by components via their <c>TagCodes</c>. Tags whose code already
    /// exists in the database are reused; new ones are created.
    /// </summary>
    public IReadOnlyCollection<TagDto> Tags { get; init; } = [];

    /// <summary>
    /// The components to upsert.
    /// </summary>
    public required IReadOnlyCollection<UnitComponentDto> Components { get; init; }

    /// <summary>
    /// Directed component upgrade links to upsert. The list is authoritative per predecessor: for every
    /// predecessor code mentioned here its existing links are replaced by exactly those listed; a
    /// predecessor not mentioned is left untouched.
    /// </summary>
    public IReadOnlyCollection<ComponentSupersessionDto> ComponentSupersessions { get; init; } = [];
}
