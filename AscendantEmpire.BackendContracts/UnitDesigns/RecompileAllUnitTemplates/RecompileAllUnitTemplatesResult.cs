namespace AscendantEmpire.BackendContracts.UnitDesigns.RecompileAllUnitTemplates;

/// <summary>
/// The outcome of a bulk recompilation of every unit template's compiled snapshot.
/// </summary>
/// <remarks>
/// Recompilation is best-effort per template: a template whose assembly no longer validates is reported in
/// <see cref="Failures"/> without blocking the rest, so the counts always add up to every template visited.
/// </remarks>
public record RecompileAllUnitTemplatesResult
{
    /// <summary>How many templates were successfully recompiled.</summary>
    public required int RecompiledCount { get; init; }

    /// <summary>Per-template failure messages for templates that could not be recompiled.</summary>
    public required IReadOnlyCollection<string> Failures { get; init; }
}
