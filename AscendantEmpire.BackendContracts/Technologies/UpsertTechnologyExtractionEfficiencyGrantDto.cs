namespace AscendantEmpire.BackendContracts.Technologies;

/// <summary>
/// One extraction efficiency modifier a Technology grants when it is discovered, as a designer authors it.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="FlatValue"/> and <see cref="PercentValue"/> are whole numbers, not scaled ones.</b> <see cref="FlatValue"/> of <c>95</c> means ninety-five
/// percentage points and is stored as <c>950</c>; <see cref="PercentValue"/> of <c>20</c> means twenty percent of the starting efficiency and is stored as
/// <c>200</c>. The handler scales both, so nothing outside it ever writes a raw value.
/// </para>
/// <para>
/// At least one must be non-zero, which mirrors <c>CK_TechnologyExtractionEfficiencyGrant_HasValue</c>. Either may be negative, for a Technology that
/// makes an Asset harder to extract. The Asset must be naturally occurring, because nothing extracts a made Asset from the land.
/// </para>
/// </remarks>
public record UpsertTechnologyExtractionEfficiencyGrantDto
{
    /// <summary>
    /// The code of the naturally occurring Asset whose extraction efficiency is modified.
    /// </summary>
    public required string AssetCode { get; init; }

    /// <summary>
    /// Whole percentage points added to the efficiency. Negative for a penalty; 0 when this grant has no flat part.
    /// </summary>
    public int FlatValue { get; init; }

    /// <summary>
    /// A whole percentage of the starting efficiency added to the efficiency. Negative for a penalty; 0 when this grant has no percentage part.
    /// </summary>
    public int PercentValue { get; init; }
}
