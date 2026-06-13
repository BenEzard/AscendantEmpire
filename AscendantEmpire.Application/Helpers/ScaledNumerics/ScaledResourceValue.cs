namespace AscendantEmpire.Application.Helpers.ScaledNumerics;

/// <summary>
/// Converts nullable resource values between the whole numbers used by the UI/API and the scaled raw
/// values stored in the database.
/// </summary>
/// <remarks>
/// <para>
/// The UI works in whole numbers (e.g. a designer enters 50), while the database stores the value scaled
/// by <see cref="ScaledAmount.Scale"/> (e.g. 50,000) so downstream fixed-point bonus math keeps sub-unit
/// precision. These helpers wrap that round trip and preserve <c>null</c> (resource not applicable).
/// </para>
/// </remarks>
public static class ScaledResourceValue
{
    /// <summary>
    /// Converts a whole resource value from the UI into its scaled raw storage value.
    /// </summary>
    /// <remarks>Null (resource not applicable) is preserved as null.</remarks>
    public static long? ToRaw(int? wholeValue)
    {
        return wholeValue is null ? null : ScaledAmount.FromWhole(wholeValue.Value).RawValue;
    }

    /// <summary>
    /// Converts a scaled raw storage value back into the whole number shown in the UI.
    /// </summary>
    /// <remarks>Null (resource not applicable) is preserved as null.</remarks>
    public static int? ToWhole(long? scaledRaw)
    {
        return scaledRaw is null ? null : (int)ScaledAmount.FromRaw(scaledRaw.Value).ToWholeRoundedDown();
    }
}
