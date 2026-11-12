namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// One ongoing need a component has, as a quantity of one supply set.
/// </summary>
/// <remarks>
/// What the quantity is measured per comes from the set's <c>SupplyType</c>, so there is no basis to carry here.
/// </remarks>
public record UnitComponentSupplyRequirementDto
{
    /// <summary>
    /// Code of the supply set this need draws on. Must resolve to a set in the database.
    /// </summary>
    public required string SupplySetCode { get; init; }

    /// <summary>
    /// How much of the set is needed, scaled by <c>ScaledAmount.Scale</c> (1,000). Must be above zero.
    /// </summary>
    public required long Quantity { get; init; }
}
