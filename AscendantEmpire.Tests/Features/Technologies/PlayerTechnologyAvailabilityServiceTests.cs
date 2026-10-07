using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests the one owner of whether a Player's technology is Locked or Available.
/// </summary>
/// <remarks>
/// <para>
/// In-memory: the service is a graph walk over rows it loads itself. The three cases worth the most here are the <b>optional-prerequisite</b> rule, which the
/// design doc states and nothing else enforces; <b>idempotency</b>, which is what makes the reconcile safe to call freely and every race on it benign; and
/// <b>backfill</b>, which is the whole reason this is a service rather than a step inside the discovery handler.
/// </para>
/// <para>
/// Unlocked and Discovered rows are deliberately never touched, and that is asserted rather than assumed — a technology must not become unreachable again
/// because somebody re-authored its prerequisites.
/// </para>
/// </remarks>
[TestFixture]
internal class PlayerTechnologyAvailabilityServiceTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;

    private AscendantEmpireContext _context = null!;
    private PlayerTechnologyAvailabilityService _service = null!;
    private int _playerId;

    [SetUp]
    public async Task SetUpService()
    {
        _context = await DbFactory.CreateDbContextAsync();
        _service = new PlayerTechnologyAvailabilityService(NullLogger<PlayerTechnologyAvailabilityService>.Instance);

        _context.GameIndex.Add(new GameIndex
        {
            Id = _GAME_ID,
            GameName = "Fixture",
            CampaignMapIndexId = 1,
            NumberOfPlayers = 1,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.AwaitingPlayers,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = 30
        });

        var player = new Player
        {
            GameId = _GAME_ID,
            RulerTitle = "Chief",
            RulerName = "Fixture",
            FactionName = "Fixture",
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = "/portraits/ruler_01.png"
        };

        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        _playerId = player.Id;
    }

    [TearDown]
    public void DisposeContext()
    {
        _context?.Dispose();
    }

    [Test, Description("A technology with no prerequisites is available, so a tree always has a way in.")]
    public async Task ShouldMakeARootAvailable()
    {
        await SeedCatalogueAsync([Technology("Root")]);
        await SeedPlayerRowsAsync();

        await ReconcileAsync();

        Assert.That(await StatusOfAsync("Root"), Is.EqualTo(TechnologyStatus.Available));
    }

    [Test, Description("A technology whose mandatory prerequisite is undiscovered stays locked, which is most of the tree on turn one.")]
    public async Task ShouldLeaveATechnologyWithAnUnmetMandatoryPrerequisiteLocked()
    {
        await SeedCatalogueAsync(
        [
            Technology("Root"),
            Technology("Child") with { Prerequisites = [Mandatory("Root")] }
        ]);
        await SeedPlayerRowsAsync();

        await ReconcileAsync();

        Assert.That(await StatusOfAsync("Child"), Is.EqualTo(TechnologyStatus.Locked));
    }

    [Test, Description("Discovering a prerequisite makes its dependent available, which is the discovery cascade the whole tree advances by.")]
    public async Task ShouldMakeADependentAvailableOnceItsPrerequisiteIsDiscovered()
    {
        await SeedCatalogueAsync(
        [
            Technology("Root"),
            Technology("Child") with { Prerequisites = [Mandatory("Root")] }
        ]);
        await SeedPlayerRowsAsync();
        await MarkDiscoveredAsync("Root");

        await ReconcileAsync();

        Assert.That(await StatusOfAsync("Child"), Is.EqualTo(TechnologyStatus.Available));
    }

    [Test, Description("All mandatory prerequisites must be met, not merely one, so a technology needing two is still locked on one.")]
    public async Task ShouldRequireEveryMandatoryPrerequisite()
    {
        await SeedCatalogueAsync(
        [
            Technology("First"),
            Technology("Second"),
            Technology("Child") with { Prerequisites = [Mandatory("First"), Mandatory("Second")] }
        ]);
        await SeedPlayerRowsAsync();
        await MarkDiscoveredAsync("First");

        await ReconcileAsync();

        Assert.That(await StatusOfAsync("Child"), Is.EqualTo(TechnologyStatus.Locked));

        await MarkDiscoveredAsync("Second");
        await ReconcileAsync();

        Assert.That(await StatusOfAsync("Child"), Is.EqualTo(TechnologyStatus.Available));
    }

    [Test, Description("One of several optional prerequisites is enough, which is the rule the optional kind exists for.")]
    public async Task ShouldAcceptOneOfSeveralOptionalPrerequisites()
    {
        await SeedCatalogueAsync(
        [
            Technology("FirstWay"),
            Technology("SecondWay"),
            Technology("Child") with { Prerequisites = [Optional("FirstWay"), Optional("SecondWay")] }
        ]);
        await SeedPlayerRowsAsync();
        await MarkDiscoveredAsync("SecondWay");

        await ReconcileAsync();

        Assert.That(await StatusOfAsync("Child"), Is.EqualTo(TechnologyStatus.Available));
    }

    [Test, Description("No optional prerequisite met leaves the technology locked, so optional means 'one of these' rather than 'ignorable'.")]
    public async Task ShouldLockATechnologyWithNoOptionalPrerequisiteMet()
    {
        await SeedCatalogueAsync(
        [
            Technology("FirstWay"),
            Technology("SecondWay"),
            Technology("Child") with { Prerequisites = [Optional("FirstWay"), Optional("SecondWay")] }
        ]);
        await SeedPlayerRowsAsync();

        await ReconcileAsync();

        Assert.That(await StatusOfAsync("Child"), Is.EqualTo(TechnologyStatus.Locked));
    }

    [Test, Description("Mandatory and optional prerequisites both apply together: every mandatory plus at least one optional.")]
    public async Task ShouldRequireEveryMandatoryAndOneOptionalTogether()
    {
        await SeedCatalogueAsync(
        [
            Technology("Must"),
            Technology("EitherA"),
            Technology("EitherB"),
            Technology("Child") with { Prerequisites = [Mandatory("Must"), Optional("EitherA"), Optional("EitherB")] }
        ]);
        await SeedPlayerRowsAsync();

        // The optional arm alone is not enough while the mandatory one is unmet.
        await MarkDiscoveredAsync("EitherA");
        await ReconcileAsync();

        Assert.That(await StatusOfAsync("Child"), Is.EqualTo(TechnologyStatus.Locked));

        await MarkDiscoveredAsync("Must");
        await ReconcileAsync();

        Assert.That(await StatusOfAsync("Child"), Is.EqualTo(TechnologyStatus.Available));
    }

    [Test, Description("An Unlocked row is left alone, because paying the acquisition cost is a fact about what the Player did, not a derivation.")]
    public async Task ShouldNotTouchAnUnlockedRow()
    {
        await SeedCatalogueAsync([Technology("Root")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Root", TechnologyStatus.Unlocked, acquisitionPaidOnRound: 3);

        await ReconcileAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await StatusOfAsync("Root"), Is.EqualTo(TechnologyStatus.Unlocked));
            Assert.That((await RowAsync("Root")).AcquisitionPaidOnRound, Is.EqualTo(3));
        });
    }

    [Test, Description("A Discovered row survives its prerequisites being re-authored, so a catalogue edit cannot take a technology back off a Player.")]
    public async Task ShouldNotRelockADiscoveredTechnologyWhenItsPrerequisitesChange()
    {
        await SeedCatalogueAsync([Technology("Root"), Technology("Child")]);
        await SeedPlayerRowsAsync();
        await MarkDiscoveredAsync("Child");

        // Child now needs something the Player has not discovered, which it did not need when they discovered it.
        await SeedCatalogueAsync([Technology("Child") with { Prerequisites = [Mandatory("Root")] }]);

        await ReconcileAsync();

        Assert.That(await StatusOfAsync("Child"), Is.EqualTo(TechnologyStatus.Discovered));
    }

    [Test, Description("A second reconcile changes nothing, which is what makes it safe to call freely and every race on it benign.")]
    public async Task ShouldBeIdempotent()
    {
        await SeedCatalogueAsync(
        [
            Technology("Root"),
            Technology("Child") with { Prerequisites = [Mandatory("Root")] }
        ]);
        await SeedPlayerRowsAsync();
        await MarkDiscoveredAsync("Root");

        int firstPass = await ReconcileAsync();
        int secondPass = await ReconcileAsync();

        Assert.Multiple(() =>
        {
            Assert.That(firstPass, Is.GreaterThan(0));
            Assert.That(secondPass, Is.Zero, "nothing was left to put right");
        });
    }

    [Test, Description("A technology seeded after the game was set up is backfilled, which is the only way back from a catalogue edit mid-game.")]
    public async Task ShouldBackfillATechnologySeededAfterSetup()
    {
        await SeedCatalogueAsync([Technology("Root")]);
        await SeedPlayerRowsAsync();
        await ReconcileAsync();

        // A new technology arrives in a catalogue the Player's game was set up before.
        await SeedCatalogueAsync([Technology("LaterArrival") with { DirectedRequired = 250 }]);

        int changed = await ReconcileAsync();

        PlayerTechnology backfilled = await RowAsync("LaterArrival");

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.GreaterThan(0));
            Assert.That(backfilled.GameId, Is.EqualTo(_GAME_ID));
            Assert.That(backfilled.DirectedRequired, Is.EqualTo(250));
            Assert.That(backfilled.DirectedPoints, Is.Zero);

            // It has no prerequisites, so the same pass that created it also found it available.
            Assert.That(backfilled.Status, Is.EqualTo(TechnologyStatus.Available));
        });
    }

    [Test, Description("A whole game reconciles every player independently, so one player's discoveries never unlock another's tree.")]
    public async Task ShouldReconcileEveryPlayerInTheGameIndependently()
    {
        var second = new Player
        {
            GameId = _GAME_ID,
            RulerTitle = "Chief",
            RulerName = "Second",
            FactionName = "Second",
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = "/portraits/ruler_01.png"
        };

        _context.Players.Add(second);
        await _context.SaveChangesAsync();

        await SeedCatalogueAsync(
        [
            Technology("Root"),
            Technology("Child") with { Prerequisites = [Mandatory("Root")] }
        ]);

        await SeedPlayerRowsAsync();
        await SeedPlayerRowsAsync(second.Id);
        await MarkDiscoveredAsync("Root");

        await _service.ReconcileGameAsync(_context, _GAME_ID, CancellationToken.None);
        await _context.SaveChangesAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await StatusOfAsync("Child"), Is.EqualTo(TechnologyStatus.Available));
            Assert.That(await StatusOfAsync("Child", second.Id), Is.EqualTo(TechnologyStatus.Locked));
        });
    }

    // --- Helpers ---------------------------------------------------------------------

    private static UpsertTechnologyDto Technology(string code)
    {
        return new UpsertTechnologyDto
        {
            Code = code,
            DisplayName = code,
            Era = TechnologyEra.Stone,
            DirectedRequired = 100
        };
    }

    private static UpsertTechnologyPrerequisiteDto Mandatory(string code)
    {
        return new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = code };
    }

    private static UpsertTechnologyPrerequisiteDto Optional(string code)
    {
        return new UpsertTechnologyPrerequisiteDto
        {
            PrerequisiteTechnologyCode = code,
            PrerequisiteKind = TechnologyPrerequisiteKind.Optional
        };
    }

    private async Task SeedCatalogueAsync(IReadOnlyCollection<UpsertTechnologyDto> technologies)
    {
        var handler = new UpsertTechnologiesHandler(NullLogger<UpsertTechnologiesHandler>.Instance, _context);

        ServiceResultBase result = await handler.Handle(new UpsertTechnologiesRequest(technologies), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
    }

    /// <summary>
    /// Writes a Locked row for every catalogue technology, as game setup's generator would.
    /// </summary>
    private async Task SeedPlayerRowsAsync(int? playerId = null)
    {
        int targetPlayerId = playerId ?? _playerId;

        int[] alreadyHeld = await _context.PlayerTechnologies
            .Where(row => row.PlayerId == targetPlayerId)
            .Select(row => row.TechnologyId)
            .ToArrayAsync();

        List<Technology> catalogue = await _context.Technologies
            .Where(technology => !alreadyHeld.Contains(technology.Id))
            .ToListAsync();

        _context.PlayerTechnologies.AddRange(catalogue.Select(technology => new PlayerTechnology
        {
            PlayerId = targetPlayerId,
            TechnologyId = technology.Id,
            GameId = _GAME_ID,
            Status = TechnologyStatus.Locked,
            DirectedRequired = technology.DirectedRequired,
            ExperienceRequired = technology.ExperienceRequired,
            DiffusionRequired = technology.DiffusionRequired
        }));

        await _context.SaveChangesAsync();
    }

    private async Task<int> ReconcileAsync()
    {
        int changed = await _service.ReconcileAsync(_context, _GAME_ID, _playerId, CancellationToken.None);
        await _context.SaveChangesAsync();

        return changed;
    }

    private async Task MarkDiscoveredAsync(string code)
    {
        await SetStatusAsync(code, TechnologyStatus.Discovered, discoveredOnRound: 1);
    }

    private async Task SetStatusAsync(
        string code,
        TechnologyStatus status,
        int? acquisitionPaidOnRound = null,
        int? discoveredOnRound = null)
    {
        PlayerTechnology row = await RowAsync(code);

        row.Status = status;
        row.AcquisitionPaidOnRound = acquisitionPaidOnRound;
        row.DiscoveredOnRound = discoveredOnRound;

        await _context.SaveChangesAsync();
    }

    private async Task<PlayerTechnology> RowAsync(string code, int? playerId = null)
    {
        int technologyId = await _context.Technologies.Where(t => t.Code == code).Select(t => t.Id).SingleAsync();

        return await _context.PlayerTechnologies.SingleAsync(row => row.PlayerId == (playerId ?? _playerId) && row.TechnologyId == technologyId);
    }

    private async Task<TechnologyStatus> StatusOfAsync(string code, int? playerId = null)
    {
        return (await RowAsync(code, playerId)).Status;
    }
}
