namespace AscendantEmpire.BackendContracts.UnitDesigns.CatalogTransfer;

/// <summary>
/// Selects which parts of the unit-design catalogue an export job should include.
/// </summary>
/// <remarks>
/// <para>The checkbox list in the Entity Setup Tool maps onto these flags and is expected to grow over
/// time. At least one flag must be true.</para>
/// <para>Ticking <see cref="Templates"/> always pulls in the components each catalogue template needs,
/// even when <see cref="Components"/> is false; ticking <see cref="Components"/> exports the entire
/// component catalogue.</para>
/// </remarks>
public record InitiateCatalogExportDto
{
    /// <summary>
    /// Export the entire component catalogue (every component, the tags they reference and the
    /// component-supersession links among them).
    /// </summary>
    public bool Components { get; init; }

    /// <summary>
    /// Export every catalogue (global/common) template, plus the components each one needs.
    /// </summary>
    public bool Templates { get; init; }
}
