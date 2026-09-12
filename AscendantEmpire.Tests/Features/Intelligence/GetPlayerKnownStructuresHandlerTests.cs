using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Intelligence.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerKnownStructures;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;
using static AscendantEmpire.Tests.Features.Intelligence.IntelligenceReadTestData;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Tests for <see cref="GetPlayerKnownStructuresHandler"/>: which known Structures a Player is shown, where and when, and that they leave stripped to their fidelity.
/// </summary>
/// <remarks>
/// In-memory, because the read is plain LINQ over the known Structures and the Turns. The Game and its Turns are described on <see cref="IntelligenceReadTestData"/>.
/// </remarks>
[TestFixture]
internal class GetPlayerKnownStructuresHandlerTests : InMemoryTestBase
{
    private GetPlayerKnownStructuresHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandlerAsync()
    {
        await SeedGameAsync(DbFactory);

        _handler = new GetPlayerKnownStructuresHandler(NullLogger<GetPlayerKnownStructuresHandler>.Instance, DbFactory, new SightDataReader());
    }

    private async Task<IReadOnlyCollection<KnownStructure>> ReadKnownStructuresAsync(int playerId)
    {
        ServiceResult<IReadOnlyCollection<KnownStructure>> result = await _handler.Handle(new GetPlayerKnownStructuresRequest(GAME_ID, playerId), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    [Test, Description("A Player sees only their own memories, each placed on its tile with the Round it was last seen in and whether it still stood, which is what the client draws and ages.")]
    public async Task ShouldReturnOnlyTheReadersKnownStructuresWithWhereAndWhenTheyWereLastSeenAsync()
    {
        await AddAsync(
            DbFactory,
            KnownStructure(1, READER_PLAYER_ID, structureInstanceId: 60, ROUND_TWO_ACTING_TURN_ID, tile: (6, 3), SightingFidelity.Detail, KnownStructureStatus.Razed),
            KnownStructure(2, OTHER_PLAYER_ID, structureInstanceId: 60, ROUND_ONE_ACTING_TURN_ID, tile: (6, 3), SightingFidelity.Detail));

        KnownStructure knownStructure = (await ReadKnownStructuresAsync(READER_PLAYER_ID)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((knownStructure.Location.X, knownStructure.Location.Y), Is.EqualTo((6, 3)));
            Assert.That(knownStructure.LastSeenRoundNumber, Is.EqualTo(2));
            Assert.That(knownStructure.LastSeenPlayerTurnId, Is.EqualTo(ROUND_TWO_ACTING_TURN_ID));
            Assert.That(knownStructure.Status, Is.EqualTo(KnownStructureStatus.Razed));
            Assert.That(knownStructure.StructureInstanceId, Is.EqualTo(60));
            Assert.That(knownStructure.StructureId, Is.EqualTo(7));
            Assert.That(knownStructure.OwnerPlayerId, Is.EqualTo(ACTING_PLAYER_ID));
            Assert.That(knownStructure.CompletionPercent, Is.EqualTo(80));
        }
    }

    [Test, Description("Known Structures go through the same redaction as reports, so the map layer cannot hand over what the feed withholds.")]
    public async Task ShouldStripKnownStructuresDownToTheirFidelityAsync()
    {
        await AddAsync(DbFactory, KnownStructure(1, READER_PLAYER_ID, structureInstanceId: 60, ROUND_TWO_ACTING_TURN_ID, tile: (6, 3), SightingFidelity.Movement));

        KnownStructure knownStructure = (await ReadKnownStructuresAsync(READER_PLAYER_ID)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(knownStructure.StructureId, Is.Null);
            Assert.That(knownStructure.OwnerPlayerId, Is.Null);
            Assert.That(knownStructure.StructureInstanceId, Is.Null);
            Assert.That(knownStructure.CompletionPercent, Is.Null);
        }
    }

    [Test, Description("A Player that is not in the Game is answered as not found rather than with an empty map layer.")]
    public async Task ShouldReturnNotFoundWhenThePlayerIsNotInTheGameAsync()
    {
        ServiceResult<IReadOnlyCollection<KnownStructure>> result = await _handler.Handle(new GetPlayerKnownStructuresRequest(GAME_ID, OUTSIDER_PLAYER_ID), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }
}
