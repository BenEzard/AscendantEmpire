namespace AscendantEmpire.Tests;

/// <summary>
/// The ruler portrait paths the UI ships, for a fixture that wants a real one rather than an invented string.
/// </summary>
/// <remarks>
/// <para>
/// Kept in step by hand with the UI's RulerPortrait.ts. Nothing server-side resolves one of these to a file - the column holds a client URL, and the
/// request validator checks only its shape - so what this buys is that a fixture names a portrait that exists, and that a reader can tell a real path
/// from a made-up one.
/// </para>
/// <para>
/// Listed in file order. A chosen portrait is unique within a Game, so a fixture seeding several players needs a distinct path for each, which is what
/// <see cref="ForIndex"/> is for.
/// </para>
/// </remarks>
internal static class RulerPortraitPaths
{
    /// <summary>Every shipped portrait, in file order.</summary>
    public static readonly string[] All =
    [
        "/portraits/01_western_king.png",
        "/portraits/02_western_queen.png",
        "/portraits/03_east_asian_ruler.png",
        "/portraits/04_egyptian_queen.png",
        "/portraits/05_northern_king.png",
        "/portraits/06_african_king.png",
        "/portraits/07_northern_queen.png",
        "/portraits/08_turbaned_ruler.png",
        "/portraits/09_east_asian_queen.png",
        "/portraits/10_roman_ruler.png",
        "/portraits/11_tribal_queen.png",
        "/portraits/12_viking_king.png",
        "/portraits/13_african_queen.png",
        "/portraits/14_desert_ruler.png",
        "/portraits/15_steppe_ruler.png",
        "/portraits/16_imperial_ruler.png",
        "/portraits/17_blonde_queen.png",
        "/portraits/18_red_headband_ruler.png",
        "/portraits/19_red_hat_ruler.png",
        "/portraits/20_blue_crowned_queen.png",
    ];

    /// <summary>The portraits of female rulers, for a fixture whose definitions all assert <c>RulerGender.Female</c>.</summary>
    /// <remarks>
    /// The eight of them, against twelve male. The UI offers one gender's faces at a time, so a fixture that gives every slot the same gender and a
    /// portrait from the other half describes a pair the dialog would never have produced.
    /// </remarks>
    public static readonly string[] Female =
    [
        "/portraits/02_western_queen.png",
        "/portraits/04_egyptian_queen.png",
        "/portraits/07_northern_queen.png",
        "/portraits/09_east_asian_queen.png",
        "/portraits/11_tribal_queen.png",
        "/portraits/13_african_queen.png",
        "/portraits/17_blonde_queen.png",
        "/portraits/20_blue_crowned_queen.png",
    ];

    /// <summary>
    /// One portrait per <paramref name="index"/>, wrapping once the list runs out, so a caller numbering its players gets a different path for each.
    /// </summary>
    /// <remarks>
    /// Wrapping rather than throwing because the callers number their players by database id, not from zero. Two ids twenty apart in the same Game
    /// would collide, which no fixture does today; a caller needing a guarantee should index <see cref="All"/> itself.
    /// </remarks>
    public static string ForIndex(int index)
    {
        return All[Math.Abs(index) % All.Length];
    }
}
