namespace AscendantEmpire.Domain.Enums.Games;

/// <summary>
/// The gender a player has chosen for their ruler, used to pick which set of ruler titles they are offered.
/// </summary>
/// <remarks>
/// <para>
/// The values are stated explicitly because they persist to the database as ints; renumbering them would silently reinterpret every existing row.
/// </para>
/// <para>
/// Starting at 1 rather than 0 is deliberate: it means a missing or defaulted value on an incoming request fails validation instead of quietly
/// arriving as <see cref="Male"/>.
/// </para>
/// <para>
/// The API serialises enums by name, so this reaches the frontend as "Male" or "Female".
/// </para>
/// </remarks>
public enum RulerGender
{
    Male = 1,
    Female = 2,
}
