namespace AscendantEmpire.Domain.Enums.Assets;

/// <summary>
/// The kind of ongoing need a <c>SupplySet</c> satisfies.
/// </summary>
/// <remarks>
/// <para>
/// The type sits on the set rather than on the requirement that names it, because it is what decides how the need is measured. A requirement therefore
/// carries a quantity and nothing else: the set it points at says what that quantity is per.
/// </para>
/// <para>
/// Values are assigned explicitly and stored as <c>int</c>. Serialised as the member name, since the API registers <c>JsonStringEnumConverter</c>.
/// </para>
/// </remarks>
public enum SupplyType
{
    /// <summary>
    /// Food, measured per troop per Turn. Any member of the set feeds a Unit as well as any other.
    /// </summary>
    Food = 1,

    /// <summary>
    /// Fuel, measured per Movement Point. Members are interchangeable but differ in how far they go, which is their <c>AmountPerUnit</c>.
    /// </summary>
    Fuel = 2,

    /// <summary>
    /// Repair materials, measured per point of damage. Unlike Food and Fuel, one member is drawn at the moment damage is taken and the choice is then
    /// fixed — a Unit owing steel plate cannot pay in circuitry.
    /// </summary>
    Repair = 3
}
