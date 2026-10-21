using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.BackendContracts.Technologies;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests that the hand-authored technology catalogue is internally coherent and starts a game the way the design says it should.
/// </summary>
/// <remarks>
/// <para>
/// No database — <see cref="TechnologySeedData"/> is a static builder and every check here is over the payload it returns, before any handler sees it.
/// </para>
/// <para>
/// These pin what the catalogue cannot enforce for itself: which technologies a Player may research in their first Round, that every experience type a
/// technology names actually exists, and that the prerequisite graph is well formed. All of it used to be guaranteed by the generator that wrote this file; now
/// that it is authored by hand, these tests are what stands in its place. The sibling fixtures do the same for the starter Assets and unit templates.
/// </para>
/// <para>
/// The graph checks deliberately repeat validation <c>UpsertTechnologiesHandler</c> already performs on every request. The handler's own fixture needs a
/// database and fails with a rejected batch; these run with none, and name the offending codes.
/// </para>
/// <para>
/// The equivalent end-to-end assertions live in <c>StarterTechnologyTests</c>, which runs real game setup against the real seed - but that fixture needs the
/// SQL Server container, so it cannot be relied on to catch a rename in a routine run. These cost nothing and always run.
/// </para>
/// </remarks>
[TestFixture]
internal class TechnologySeedDataTests
{
    /// <summary>
    /// The technologies a Player may research in their first Round, which is every technology authored without a prerequisite.
    /// </summary>
    /// <remarks>
    /// Spelled out here rather than in <see cref="TechnologySeedData"/> because this is the design intent the catalogue is measured against, not a value the
    /// catalogue holds — the seed data classes carry rows and nothing else.
    /// </remarks>
    private static readonly string[] _STARTING_AVAILABLE_TECHNOLOGY_CODES =
    [
        "PrimitiveCrafting1",
        "FireMaking",
        "IntensiveGathering",
        "Language",
        "NaturalExploration"
    ];

    private readonly IReadOnlyCollection<UpsertTechnologyDto> _technologies = TechnologySeedData.Build();
    private readonly IReadOnlyCollection<UpsertExperienceTypeDto> _experienceTypes = ExperienceTypeSeedData.Build();

    [Test, Description("Exactly the six intended technologies are authored without a prerequisite, since that is what decides a Player's opening research choice.")]
    public void ShouldLeaveOnlyTheStartingTechnologiesWithoutPrerequisites()
    {
        string[] withoutPrerequisites = [.. _technologies
            .Where(technology => technology.Prerequisites.Count == 0)
            .Select(technology => technology.Code)];

        Assert.That(withoutPrerequisites, Is.EquivalentTo(_STARTING_AVAILABLE_TECHNOLOGY_CODES));
    }

    [Test, Description("Every experience type code a technology names resolves to a seeded ExperienceType, so a typo fails here rather than rejecting the whole seed batch.")]
    public void ShouldResolveEveryExperienceTypeCodeAgainstTheSeededCatalogue()
    {
        IEnumerable<string> definedCodes = _experienceTypes.Select(experienceType => experienceType.Code);

        string[] undefinedCodes = [.. _technologies
            .SelectMany(technology => technology.ExperienceTypeCodes)
            .Distinct()
            .Except(definedCodes)];

        Assert.That(undefinedCodes, Is.Empty, $"Experience type codes named by a technology but not seeded: {string.Join(", ", undefinedCodes)}");
    }

    [Test, Description("Every starter technology code game setup demands resolves to a seeded Technology, which guards a set that is empty today but breaks game creation the moment it is not.")]
    public void ShouldSeedATechnologyForEveryStarterCode()
    {
        IEnumerable<string> seededCodes = _technologies.Select(technology => technology.Code);

        // Case-insensitive, to match how GameSetupService itself tests for a missing starter - a code that differs only in case resolves there and must here too.
        string[] missingCodes = [.. GameSetupService.StarterTechnologyCodes.Except(seededCodes, StringComparer.OrdinalIgnoreCase)];

        Assert.That(missingCodes, Is.Empty, $"Starter technology codes with no seeded Technology: {string.Join(", ", missingCodes)}");
    }

    // --- The prerequisite graph ------------------------------------------------------

    [Test, Description("No two technologies share a code, because the upsert matches rows on it and rejects the whole batch when one repeats.")]
    public void ShouldAuthorEveryTechnologyCodeOnlyOnce()
    {
        string[] repeatedCodes = [.. _technologies
            .GroupBy(technology => technology.Code)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()];

        Assert.That(repeatedCodes, Is.Empty, $"Technology codes authored more than once: {string.Join(", ", repeatedCodes)}");
    }

    [Test, Description("No two technologies share a display name, because the upsert rejects a repeat and two identically labelled nodes cannot be told apart in the tree viewer.")]
    public void ShouldAuthorEveryDisplayNameOnlyOnce()
    {
        string[] repeatedDisplayNames = [.. _technologies
            .GroupBy(technology => technology.DisplayName)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()];

        Assert.That(repeatedDisplayNames, Is.Empty, $"Display names authored more than once: {string.Join(", ", repeatedDisplayNames)}");
    }

    [Test, Description("No technology names itself as a prerequisite, which is a cycle of length one and would leave it undiscoverable by any route.")]
    public void ShouldNotMakeATechnologyItsOwnPrerequisite()
    {
        string[] selfPrerequisites = [.. _technologies
            .Where(technology => technology.Prerequisites.Any(prerequisite => prerequisite.PrerequisiteTechnologyCode == technology.Code))
            .Select(technology => technology.Code)
            .Order()];

        Assert.That(selfPrerequisites, Is.Empty, $"Technologies naming themselves as a prerequisite: {string.Join(", ", selfPrerequisites)}");
    }

    [Test, Description("No technology names the same prerequisite twice, since the edge is keyed on the pair and the repeat is rejected rather than quietly collapsed.")]
    public void ShouldNameEachPrerequisiteOnlyOncePerTechnology()
    {
        string[] withRepeatedPrerequisites = [.. _technologies
            .Where(technology => technology.Prerequisites
                .GroupBy(prerequisite => prerequisite.PrerequisiteTechnologyCode)
                .Any(group => group.Count() > 1))
            .Select(technology => technology.Code)
            .Order()];

        Assert.That(withRepeatedPrerequisites, Is.Empty, $"Technologies naming the same prerequisite more than once: {string.Join(", ", withRepeatedPrerequisites)}");
    }

    [Test, Description("Every prerequisite code resolves to a technology in the catalogue, so a typo or a deleted technology fails here rather than rejecting the whole seed batch.")]
    public void ShouldResolveEveryPrerequisiteCodeAgainstTheCatalogue()
    {
        IEnumerable<string> authoredCodes = _technologies.Select(technology => technology.Code);

        // Ordinal, because the handler resolves a prerequisite by exact code - a code differing only in case does not resolve there, so it must not here.
        string[] unresolvableCodes = [.. _technologies
            .SelectMany(technology => technology.Prerequisites)
            .Select(prerequisite => prerequisite.PrerequisiteTechnologyCode)
            .Except(authoredCodes)
            .Order()];

        Assert.That(unresolvableCodes, Is.Empty, $"Prerequisite codes named by a technology but not authored: {string.Join(", ", unresolvableCodes)}");
    }

    [Test, Description("The prerequisite graph is acyclic, because every technology in a cycle, and everything behind it, is permanently undiscoverable.")]
    public void ShouldLeaveThePrerequisiteGraphAcyclic()
    {
        HashSet<string> authoredCodes = [.. _technologies.Select(technology => technology.Code)];
        Dictionary<string, string[]> prerequisitesByCode = [];

        foreach (UpsertTechnologyDto technology in _technologies)
        {
            // Edges naming a code that is not authored are dropped rather than walked: one can never be met, so counting it would report everything behind it
            // as cyclic and bury the real failure, which ShouldResolveEveryPrerequisiteCodeAgainstTheCatalogue reports on its own.
            //
            // Assigned through the indexer rather than built with ToDictionary so that a repeated code cannot throw here - that is
            // ShouldAuthorEveryTechnologyCodeOnlyOnce's failure to report, not this test's.
            prerequisitesByCode[technology.Code] = [.. technology.Prerequisites
                .Select(prerequisite => prerequisite.PrerequisiteTechnologyCode)
                .Where(authoredCodes.Contains)];
        }

        Dictionary<string, List<string>> dependentsByCode = prerequisitesByCode.Keys.ToDictionary(code => code, _ => new List<string>());
        Dictionary<string, int> unmetCountsByCode = prerequisitesByCode.ToDictionary(entry => entry.Key, entry => entry.Value.Length);

        foreach ((string code, string[] prerequisites) in prerequisitesByCode)
        {
            foreach (string prerequisite in prerequisites)
            {
                dependentsByCode[prerequisite].Add(code);
            }
        }

        // Kahn's algorithm, mirroring UpsertTechnologiesHandler.FindPrerequisiteCycleAsync: repeatedly take the technologies whose every prerequisite has
        // already been taken. Whatever is still standing when nothing more can be taken is in a cycle, or downstream of one.
        Queue<string> discoverable = new(unmetCountsByCode.Where(entry => entry.Value == 0).Select(entry => entry.Key));

        while (discoverable.Count > 0)
        {
            string discovered = discoverable.Dequeue();

            foreach (string dependent in dependentsByCode[discovered])
            {
                unmetCountsByCode[dependent]--;

                if (unmetCountsByCode[dependent] == 0)
                {
                    discoverable.Enqueue(dependent);
                }
            }
        }

        string[] undiscoverableCodes = [.. unmetCountsByCode
            .Where(entry => entry.Value > 0)
            .Select(entry => entry.Key)
            .Order()];

        Assert.That(undiscoverableCodes, Is.Empty, $"Technologies in or behind a prerequisite cycle: {string.Join(", ", undiscoverableCodes)}");
    }
}
