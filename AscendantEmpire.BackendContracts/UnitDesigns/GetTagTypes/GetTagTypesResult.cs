namespace AscendantEmpire.BackendContracts.UnitDesigns.GetTagTypes;

/// <summary>
/// The response envelope for the "get every tag type" endpoint.
/// </summary>
public record GetTagTypesResult
{
    public required IReadOnlyCollection<TagTypeView> TagTypes { get; init; }
}
