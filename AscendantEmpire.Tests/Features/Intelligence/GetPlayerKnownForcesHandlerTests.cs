using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Intelligence.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerKnownForces;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;
using static AscendantEmpire.Tests.Features.Intelligence.IntelligenceReadTestData;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Tests for <see cref="GetPlayerKnownForcesHandler"/>: which known Forces a Player is shown, where and when, and that they leave stripped to their fidelity.
/// </summary>
/// <remarks>
/// In-memory, because the read is plain LINQ over the known Forces and the Turns. The Game and its Turns are described on <see cref="IntelligenceReadTestData"/>.
/// </remarks>
[TestFixture]
internal class GetPlayerKnownForcesHandlerTests : InMemoryTestBase
{
    private GetPlayerKnownForcesHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandlerAsync()
    {
        await SeedGameAsync(DbFactory);

        _handler = new GetPlayerKnownForcesHandler(NullLogger<GetPlayerKnownForcesHandler>.Instance, DbFactory, new SightDataReader());
    }

    private async Task<IReadOnlyCollection<KnownForce>> ReadKnownForcesAsync(int playerId)
    {
        ServiceResult<IReadOnlyCollection<KnownForce>> result = await _handler.Handle(new GetPlayerKnownForcesRequest(GAME_ID, playerId), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    [Test, Description("A Player sees only their own memories, each placed on its tile with the Round it was last seen in, which is what the client ages the marker from.")]
    public async Task ShouldReturnOnlyTheReadersKnownForcesWithWhereAndWhenTheyWereLastSeenAsync()
    {
        await AddAsync(
            DbFactory,
            KnownForce(1, READER_PLAYER_ID, unitInstanceId: 99, ROUND_TWO_ACTING_TURN_ID, lastSeenTile: (3, 2), SightingFidelity.Detail),
            KnownForce(2, OTHER_PLAYER_ID, unitInstanceId: 99, ROUND_ONE_ACTING_TURN_ID, lastSeenTile: (7, 7), SightingFidelity.Detail));

        KnownForce knownForce = (await ReadKnownForcesAsync(READER_PLAYER_ID)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((knownForce.LastSeenLocation.X, knownForce.LastSeenLocation.Y), Is.EqualTo((3, 2)));
            Assert.That(knownForce.LastSeenRoundNumber, Is.EqualTo(2));
            Assert.That(knownForce.LastSeenPlayerTurnId, Is.EqualTo(ROUND_TWO_ACTING_TURN_ID));
            Assert.That(knownForce.ExitDirection, Is.EqualTo(MoveDirection.West));
            Assert.That(knownForce.UnitInstanceId, Is.EqualTo(99));
        }
    }

    [Test, Description("Known Forces go through the same redaction as reports, so the map layer cannot hand over what the feed withholds.")]
    public async Task ShouldStripKnownForcesDownToTheirFidelityAsync()
    {
        await AddAsync(DbFactory, KnownForce(1, READER_PLAYER_ID, unitInstanceId: 99, ROUND_TWO_ACTING_TURN_ID, lastSeenTile: (3, 2), SightingFidelity.Type));

        KnownForce knownForce = (await ReadKnownForcesAsync(READER_PLAYER_ID)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(knownForce.UnitTemplateId, Is.EqualTo(3));
            Assert.That(knownForce.OwnerPlayerId, Is.Null);
            Assert.That(knownForce.UnitInstanceId, Is.Null);
            Assert.That(knownForce.HealthyTroops, Is.Null);
        }
    }

    [Test, Description("A Player that is not in the Game is answered as not found rather than with an empty map layer.")]
    public async Task ShouldReturnNotFoundWhenThePlayerIsNotInTheGameAsync()
    {
        ServiceResult<IReadOnlyCollection<KnownForce>> result = await _handler.Handle(new GetPlayerKnownForcesRequest(GAME_ID, OUTSIDER_PLAYER_ID), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }
}
