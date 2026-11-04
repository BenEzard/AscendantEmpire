namespace AscendantEmpire.BackendContracts.Assets;

/// <summary>
/// One storage class to create or update in the catalogue.
/// </summary>
/// <remarks>
/// Matched on <see cref="Code"/>, so a class whose DisplayName changes but whose code does not is renamed rather than duplicated.
/// </remarks>
public record UpsertStorageClassDto
{
    /// <summary>
    /// Unique code for the storage class, e.g. "Armoury".
    /// </summary>
    /// <remarks>
    /// Required rather than derived, unlike an Asset's: every Asset names its class by this exact string, so a reworded display name must not shift it and
    /// quietly re-file a catalogue's worth of stock.
    /// </remarks>
    public required string Code { get; init; }

    /// <summary>
    /// The name a capacity refusal uses, e.g. "Armoury".
    /// </summary>
    public required string DisplayName { get; init; }
}
