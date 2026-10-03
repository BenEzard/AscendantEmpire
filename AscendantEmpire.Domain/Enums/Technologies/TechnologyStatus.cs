namespace AscendantEmpire.Domain.Enums.Technologies;

/// <summary>
/// How far one Player has got with one Technology.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Available"/> is an addition to <c>docs/design/technology.md</c>, which names only "discovered" and "unlocked". The doc contradicts itself about
/// the state between them - it says only unlocked technologies accumulate research, and then that experience and diffusion research occur regardless of the
/// acquisition cost. <see cref="Available"/> is that middle state: the passive buckets fill, but directed research cannot start until the cost is paid.
/// </para>
/// <para>
/// Ordered so that a higher value is strictly further along, which is what lets a query ask for "at least unlocked" as a single comparison.
/// </para>
/// </remarks>
public enum TechnologyStatus
{
    /// <summary>
    /// Prerequisites are not yet satisfied. Absorbs nothing at all.
    /// </summary>
    Locked = 1,

    /// <summary>
    /// Prerequisites satisfied, acquisition cost unpaid. Accrues experience and diffusion, but not directed research.
    /// </summary>
    Available = 2,

    /// <summary>
    /// Prerequisites satisfied and acquisition cost paid. Accrues all three buckets, and may be chosen as the directed research project.
    /// </summary>
    Unlocked = 3,

    /// <summary>
    /// Every required bucket is full. Its grants have been applied and it absorbs nothing further.
    /// </summary>
    Discovered = 4
}
