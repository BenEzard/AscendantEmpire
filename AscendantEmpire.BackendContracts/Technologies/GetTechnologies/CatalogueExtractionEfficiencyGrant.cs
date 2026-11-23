namespace AscendantEmpire.BackendContracts.Technologies.GetTechnologies;

/// <summary>
/// One extraction efficiency modifier a technology grants when it is discovered.
/// </summary>
/// <remarks>
/// <see cref="FlatValue"/> and <see cref="PercentValue"/> are the <b>raw stored values</b>, scaled by <c>ScaledPercent.Scale</c> - ninety-five points read back as
/// <c>950</c> - matching how the sustenance grants are served.
/// </remarks>
public record CatalogueExtractionEfficiencyGrant
{
    public required int AssetId { get; init; }

    public required string AssetCode { get; init; }

    public required string AssetDisplayName { get; init; }

    public required int FlatValue { get; init; }

    public required int PercentValue { get; init; }
}
