namespace AscendantEmpire.Domain.Enums.Technologies;

/// <summary>
/// One of the three pools of research points a Technology requires.
/// </summary>
/// <remarks>
/// <para>
/// Every required bucket must be filled completely for a Technology to be discovered, and a Technology uses between one and three of them - an unused bucket is
/// a <see langword="null"/> requirement rather than a zero one.
/// </para>
/// <para>
/// The three are <b>not</b> interchangeable in how they arrive. Directed points are a fungible per-turn budget the Player spends. Experience points arrive
/// already typed by the action that earned them (see <c>ExperienceType</c>), and diffusion points arrive attached to a trade partner or a neighbour.
/// </para>
/// <para>
/// The buckets are columns rather than rows on <c>Technology</c> and <c>PlayerTechnology</c>, because the set of three is closed and every turn-processing read
/// wants all of them at once. This enum is for the tables that genuinely are keyed by bucket - the per-turn research history and the research modifiers.
/// </para>
/// </remarks>
public enum ResearchBucket
{
    Directed = 1,
    Experience = 2,
    Diffusion = 3
}
