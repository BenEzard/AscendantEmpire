using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests the requirement generators, which are the whole of what technology customisation means.
/// </summary>
/// <remarks>
/// No database: a generator is a pure function from the catalogue, the players and the game's settings to a set of rows. That it is pure is the point — it is
/// what lets a customisation mode be added without touching a query anywhere else.
/// </remarks>
[TestFixture]
internal class TechnologyRequirementGeneratorTests
{
    private const int _GAME_ID = 7;

    [Test, Description("A row is produced for every player and every technology, since materialised per-player requirements are what the rows exist for.")]
    public void IdenticalShouldProduceARowPerPlayerPerTechnology()
    {
        IReadOnlyCollection<PlayerTechnology> rows = Generate(
            [Technology(1, directed: 100), Technology(2, directed: 200), Technology(3, directed: 300)],
            [Player(10), Player(11)]);

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(6));
            Assert.That(rows.Select(row => (row.PlayerId, row.TechnologyId)).Distinct().Count(), Is.EqualTo(6));
        });
    }

    [Test, Description("Every row starts Locked, because which are reachable is a question about the prerequisite graph and not this generator's to answer.")]
    public void IdenticalShouldStartEveryRowLocked()
    {
        IReadOnlyCollection<PlayerTechnology> rows = Generate([Technology(1, directed: 100)], [Player(10)]);

        Assert.Multiple(() =>
        {
            Assert.That(rows.Single().Status, Is.EqualTo(TechnologyStatus.Locked));
            Assert.That(rows.Single().DirectedPoints, Is.Zero);
            Assert.That(rows.Single().AcquisitionPaidOnRound, Is.Null);
            Assert.That(rows.Single().DiscoveredOnRound, Is.Null);
        });
    }

    [Test, Description("The row carries the player's game, so a game delete can purge without going through the Player.")]
    public void IdenticalShouldCarryTheGameId()
    {
        IReadOnlyCollection<PlayerTechnology> rows = Generate([Technology(1, directed: 100)], [Player(10)]);

        Assert.That(rows.Single().GameId, Is.EqualTo(_GAME_ID));
    }

    [Test, Description("At a multiplier of 100% the catalogue's figures come through unchanged, which is the no-op case the default relies on.")]
    public void IdenticalShouldCopyTheCatalogueFiguresAtOneHundredPercent()
    {
        IReadOnlyCollection<PlayerTechnology> rows = Generate(
            [Technology(1, directed: 500, experience: 250, diffusion: 125)],
            [Player(10)],
            speedMultiplierPercent: 1_000);

        PlayerTechnology row = rows.Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.DirectedRequired, Is.EqualTo(500));
            Assert.That(row.ExperienceRequired, Is.EqualTo(250));
            Assert.That(row.DiffusionRequired, Is.EqualTo(125));
        });
    }

    [Test, Description("The speed multiplier scales every bucket, which is the one setting this mode honours.")]
    public void IdenticalShouldApplyTheSpeedMultiplier()
    {
        IReadOnlyCollection<PlayerTechnology> rows = Generate(
            [Technology(1, directed: 500, experience: 250, diffusion: 125)],
            [Player(10)],
            speedMultiplierPercent: 1_500);

        PlayerTechnology row = rows.Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.DirectedRequired, Is.EqualTo(750));
            Assert.That(row.ExperienceRequired, Is.EqualTo(375));
            Assert.That(row.DiffusionRequired, Is.EqualTo(187), "integer division truncates, which is fine for a requirement");
        });
    }

    [Test, Description("An unused bucket stays null through scaling, because null means unused and is a property of the technology rather than a number.")]
    public void IdenticalShouldPreserveAnUnusedBucketAsNull()
    {
        IReadOnlyCollection<PlayerTechnology> rows = Generate(
            [Technology(1, directed: 100)],
            [Player(10)],
            speedMultiplierPercent: 2_000);

        PlayerTechnology row = rows.Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.DirectedRequired, Is.EqualTo(200));
            Assert.That(row.ExperienceRequired, Is.Null);
            Assert.That(row.DiffusionRequired, Is.Null);
        });
    }

    [Test, Description("A multiplier small enough to round a requirement away floors it at 1, or the technology would complete the instant it unlocked.")]
    public void IdenticalShouldFloorAScaledRequirementAtOne()
    {
        IReadOnlyCollection<PlayerTechnology> rows = Generate(
            [Technology(1, directed: 10)],
            [Player(10)],
            speedMultiplierPercent: 1);

        Assert.That(rows.Single().DirectedRequired, Is.EqualTo(1));
    }

    [Test, Description("Every player gets the same figures, which is the correct answer for this mode rather than a placeholder for randomisation.")]
    public void IdenticalShouldGiveEveryPlayerTheSameFigures()
    {
        IReadOnlyCollection<PlayerTechnology> rows = Generate(
            [Technology(1, directed: 500, experience: 250)],
            [Player(10), Player(11), Player(12)],
            speedMultiplierPercent: 1_300);

        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(row => row.DirectedRequired).Distinct().Count(), Is.EqualTo(1));
            Assert.That(rows.Select(row => row.ExperienceRequired).Distinct().Count(), Is.EqualTo(1));
        });
    }

    [Test, Description("Fuzziness is ignored by this mode, since it exists to make players differ and this mode is defined by their not differing.")]
    public void IdenticalShouldIgnoreFuzziness()
    {
        IReadOnlyCollection<PlayerTechnology> rows = Generate(
            [Technology(1, directed: 500)],
            [Player(10), Player(11)],
            fuzzinessPercent: 5_000);

        Assert.That(rows.Select(row => row.DirectedRequired), Is.All.EqualTo(500));
    }

    [TestCase(TechnologyCustomisation.RandomisedEqual,
        Description = "The randomised-equal mode refuses rather than silently behaving as Identical, which would be a game played by rules nobody chose.")]
    [TestCase(TechnologyCustomisation.FullyRandomised,
        Description = "The fully-randomised mode refuses for the same reason.")]
    public void AnUnimplementedModeShouldThrowNamingItself(TechnologyCustomisation customisation)
    {
        var generator = new UnimplementedTechnologyRequirementGenerator(customisation);

        NotSupportedException? thrown = Assert.Throws<NotSupportedException>(
            () => generator.Generate([Technology(1, directed: 100)], [Player(10)], Configuration(customisation: customisation)));

        Assert.Multiple(() =>
        {
            Assert.That(generator.Customisation, Is.EqualTo(customisation));
            Assert.That(thrown!.Message, Does.Contain(customisation.ToString()));
        });
    }

    [Test, Description("Every customisation mode has a generator, so the selector never fails as 'nothing registered' for a mode that merely is not written.")]
    public void EveryCustomisationModeShouldHaveAGenerator()
    {
        ITechnologyRequirementGenerator[] generators =
        [
            new IdenticalTechnologyRequirementGenerator(),
            new UnimplementedTechnologyRequirementGenerator(TechnologyCustomisation.RandomisedEqual),
            new UnimplementedTechnologyRequirementGenerator(TechnologyCustomisation.FullyRandomised)
        ];

        Assert.That(
            generators.Select(generator => generator.Customisation).OrderBy(mode => mode),
            Is.EqualTo(Enum.GetValues<TechnologyCustomisation>().OrderBy(mode => mode)),
            "a mode with no generator would fail game setup with a message about registration rather than about the mode");
    }

    // --- Helpers ---------------------------------------------------------------------

    private static IReadOnlyCollection<PlayerTechnology> Generate(
        IReadOnlyCollection<Technology> catalogue,
        IReadOnlyCollection<Player> players,
        long speedMultiplierPercent = 1_000,
        long fuzzinessPercent = 0)
    {
        return new IdenticalTechnologyRequirementGenerator()
            .Generate(catalogue, players, Configuration(speedMultiplierPercent, fuzzinessPercent));
    }

    private static GameConfiguration Configuration(
        long speedMultiplierPercent = 1_000,
        long fuzzinessPercent = 0,
        TechnologyCustomisation customisation = TechnologyCustomisation.Identical)
    {
        return new GameConfiguration
        {
            GameId = _GAME_ID,
            EmpireBaseResearchPoints = 100,
            TechnologySpeedMultiplierPercent = speedMultiplierPercent,
            TechnologyFuzzinessPercent = fuzzinessPercent,
            TechnologyCustomisation = customisation,
            ResearchOverflowEnabled = false,
            UnspentResearchPointsBehaviour = UnspentResearchPointsBehaviour.Lost
        };
    }

    private static Technology Technology(int id, int? directed = null, int? experience = null, int? diffusion = null)
    {
        return new Technology
        {
            Id = id,
            Code = $"Technology{id}",
            DisplayName = $"Technology {id}",
            IconPath = string.Empty,
            Era = TechnologyEra.Stone,
            SortOrder = id * 10,
            DirectedRequired = directed,
            ExperienceRequired = experience,
            DiffusionRequired = diffusion
        };
    }

    private static Player Player(int id)
    {
        return new Player
        {
            Id = id,
            GameId = _GAME_ID,
            RulerTitle = "Chief",
            RulerName = $"Ruler {id}",
            FactionName = $"Faction {id}",
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = "/portraits/01_western_king.png"
        };
    }
}
