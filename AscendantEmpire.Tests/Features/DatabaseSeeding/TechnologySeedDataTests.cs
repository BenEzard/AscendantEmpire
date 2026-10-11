using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests that the hand-authored technology catalogue is internally coherent and starts a game the way the design says it should.
/// </summary>
/// <remarks>
/// <para>
/// No database — <see cref="TechnologySeedData"/> is a static builder and every check here is over the payload it returns, before any handler sees it.
/// </para>
/// <para>
/// These pin the two links the catalogue cannot enforce for itself: which technologies a Player may research in their first Round, and that every experience
/// type a technology names actually exists. Both of those used to be guaranteed by the generator that wrote this file; now that it is authored by hand, these
/// tests are what stands in its place. The sibling fixtures do the same for the starter Assets and unit templates.
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
        "Hunting",
        "FireMaking",
        "IntensiveGathering",
        "Language",
        "NaturalExploration"
    ];

    /// <summary>
    /// The placeholder root the technologies with no established prerequisites are parented to.
    /// </summary>
    private const string _BROKEN_CODE = "Broken";

    private readonly IReadOnlyCollection<UpsertTechnologyDto> _technologies = TechnologySeedData.Build();
    private readonly IReadOnlyCollection<UpsertExperienceTypeDto> _experienceTypes = ExperienceTypeSeedData.Build();

    [Test, Description("Exactly the six intended technologies and the Broken placeholder are authored without a prerequisite, since that is what decides a Player's opening research choice.")]
    public void ShouldLeaveOnlyTheStartingTechnologiesAndTheBrokenPlaceholderWithoutPrerequisites()
    {
        string[] withoutPrerequisites = [.. _technologies
            .Where(technology => technology.Prerequisites.Count == 0)
            .Select(technology => technology.Code)];

        // Broken belongs in here: it is a root by construction, which is the whole reason it can hold the technologies behind it out of reach.
        string[] expected = [.. _STARTING_AVAILABLE_TECHNOLOGY_CODES, _BROKEN_CODE];

        Assert.That(withoutPrerequisites, Is.EquivalentTo(expected));
    }

    [Test, Description("Every technology the Broken placeholder holds back names it as a mandatory prerequisite, because an optional one alone would be satisfiable.")]
    public void ShouldHoldTheBrokenPlaceholdersDependentsBackMandatorily()
    {
        UpsertTechnologyPrerequisiteDto[] brokenEdges = [.. _technologies
            .SelectMany(technology => technology.Prerequisites)
            .Where(prerequisite => prerequisite.PrerequisiteTechnologyCode == _BROKEN_CODE)];

        Assert.Multiple(() =>
        {
            Assert.That(brokenEdges, Is.Not.Empty, "nothing is parented to the placeholder, so it has no reason to exist");

            Assert.That(brokenEdges.All(edge => edge.PrerequisiteKind == TechnologyPrerequisiteKind.Mandatory), Is.True,
                "an optional edge is met by any one of a technology's optional prerequisites, so it would not hold anything back");
        });
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
}
