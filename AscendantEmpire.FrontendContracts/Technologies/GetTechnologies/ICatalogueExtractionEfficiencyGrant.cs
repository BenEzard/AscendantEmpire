namespace AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

/// <summary>
/// One extraction efficiency modifier a technology grants when it is discovered.
/// </summary>
/// <remarks>
/// Both values are raw and scaled, as the modifier tables hold them - ninety-five points read back as <c>950</c>.
/// </remarks>
public interface ICatalogueExtractionEfficiencyGrant
{
    int AssetId { get; }

    string AssetCode { get; }

    string AssetDisplayName { get; }

    int FlatValue { get; }

    int PercentValue { get; }
}
