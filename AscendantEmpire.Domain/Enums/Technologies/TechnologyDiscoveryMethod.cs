namespace AscendantEmpire.Domain.Enums.Technologies;

/// <summary>
/// How a Player came to finish a Technology.
/// </summary>
/// <remarks>
/// <para>
/// This records how the Technology was <b>finished</b>, not where all of its points came from. Conquering a capital can deliver partial progress that directed
/// research later completes, in which case the method is <see cref="DirectedResearch"/> even though most of the points arrived by <see cref="Conquest"/>. Only
/// <c>ResearchHistory</c> answers the second question.
/// </para>
/// <para>
/// The last four all have a counterparty or a victim, which <c>PlayerTechnology.DiscoveredFromPlayerId</c> names.
/// </para>
/// </remarks>
public enum TechnologyDiscoveryMethod
{
    DirectedResearch = 1,
    Experience = 2,
    Diffusion = 3,
    Trade = 4,
    Espionage = 5,
    BattlefieldCapture = 6,
    Conquest = 7
}
