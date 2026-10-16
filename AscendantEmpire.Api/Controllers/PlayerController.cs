using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Players.Classes;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.BackendContracts.Players.GetPlayers;
using AscendantEmpire.BackendContracts.Players.GetPlayerStructures;
using AscendantEmpire.BackendContracts.Assets.GetPlayerAssets;
using AscendantEmpire.BackendContracts.Units.GetPlayerForces;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Player;
using AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;
using AscendantEmpire.FrontendContracts.Units;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
// Every action here needs a game token: the game and player are read from the token's claims,
// not from the request. A user token (no game entered yet) is rejected by RequireGameContext.
[Authorize]
public class PlayerController : ControllerBase
{
    /// <summary>
    /// Get a Player's current Structures
    /// </summary>
    /// <response code="200">Player information for Campaign and Battle Maps</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("structures")]
    [ProducesResponseType(typeof(IPlayerStructureCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerStructuresAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        // Game and player come from the token, so the caller can't ask for someone else's data.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<IReadOnlyCollection<PlayerStructure>> response = await mediator.Send(new GetPlayerStructuresRequest(gameId, playerId), cancelToken);
        return response.ToActionResult(contract =>
        {
            return new DTPlayerStructureCollection
            {
                Structures = response.SuccessValue?.Select(s => new DTPlayerStructure
                {
                    CampaignMapTileId = s.CampaignMapTileId,
                    BattleMapTileId = s.BattleMapTileId,
                    XCoord = s.XCoord,
                    YCoord = s.YCoord,
                    StructureId = s.StructureId,
                    Name = s.Name,
                    Code = s.Code,
                    CompletionPercent = s.CompletionPercent,
                    OwnerPlayerId = s.OwnerPlayerId,
                    SightRange = s.SightRange,
                }).ToList() ?? []
            };
        });
    }

    /// <summary>
    /// Get every Player in the Game the caller has entered: who they are, the colour they fly, and their ruler's portrait.
    /// </summary>
    /// <remarks>
    /// Public identity only, so any Player in the Game may read it. The client uses it to name the owner of a Force in an intelligence report. Every status is
    /// included, since a report can be about a Player who has since been eliminated.
    /// </remarks>
    /// <response code="200">Every Player in the Game</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("game-players")]
    [ProducesResponseType(typeof(IGamePlayerCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetGamePlayersAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        // Only the game comes from the token: the list is the same for every Player in it.
        if (!this.TryGetGameContext(out int gameId, out int _, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<IReadOnlyCollection<Player>> response = await mediator.Send(new GetPlayersRequest(gameId), cancelToken);

        return response.ToActionResult(players => new DTGamePlayerCollection
        {
            Players =
            [
                .. (players ?? []).OrderBy(p => p.Id).Select(p => new DTGamePlayer
                {
                    PlayerId = p.Id,
                    RulerLabel = $"{p.RulerTitle} {p.RulerName} ({p.FactionName})",
                    FactionName = p.FactionName,
                    FactionColour = p.FactionColour,
                    RulerPortraitPath = p.RulerPortraitPath,
                    Status = p.Status
                })
            ]
        });
    }

    /// <summary>
    /// Get a Player's current Forces
    /// </summary>
    /// <response code="200">Player information for Campaign and Battle Maps</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("forces")]
    [ProducesResponseType(typeof(IPlayerForceCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerForcesAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        // Game and player come from the token, so the caller can't ask for someone else's data.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<IReadOnlyCollection<PlayerForce>> response = await mediator.Send(new GetPlayerForcesRequest(gameId, playerId), cancelToken);

        return response.ToActionResult(contract => response.SuccessValue?.Select(s => new DTPlayerForce
        {
            CampaignMapTileId = s.CampaignMapTileId,
            BattleMapTileId = s.BattleMapTileId,
            CampaignXCoord = s.CampaignXCoord,
            CampaignYCoord = s.CampaignYCoord,
            SightRange = s.SightRange,
            MapImagePath = s.MapImagePath,
            HasCampaignMovementRemaining = s.HasCampaignMovementRemaining,
            CampaignMovementRemaining = s.CampaignMovementRemaining,
            CampaignMovementPoints = s.CampaignMovementPoints,
            BattleMovementRemaining = s.BattleMovementRemaining
        }).ToList() ?? []);
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // Assets
    //
    // The Player's view of the asset catalogue, gated by what they have unlocked. This is a tree rather than a list, because the Assets toolbar and
    // the panels behind it are drawn from the catalogue's own hierarchy.
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Get the Assets a Player can currently see, as the tree the Assets toolbar and its panels are drawn from
    /// </summary>
    /// <remarks>
    /// One call rather than one per meta-category: the toolbar needs the unlocked meta-category list before any panel is opened, and a category's or
    /// group's visibility is a function of the Assets beneath it, so splitting it would force the client to recombine what the server already knows.
    /// An unlocked meta-category with nothing visible under it is still returned, with no categories, so its toolbar icon stays put over an empty panel.
    /// </remarks>
    /// <response code="200">The Player's visible asset tree</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">The Player is not in an in-progress Game, or that Game has no campaign turn queue to read the current Round from</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("assets")]
    [ProducesResponseType(typeof(IPlayerAssetTree), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerAssetsAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        // Game and player come from the token, so the caller can't ask for someone else's data.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<IReadOnlyCollection<PlayerAssetMetaCategory>> response =
            await mediator.Send(new GetPlayerAssetsRequest(gameId, playerId), cancelToken);

        return response.ToActionResult(metaCategories => new DTPlayerAssetTree
        {
            MetaCategories =
            [
                .. (metaCategories ?? []).Select(m => new DTPlayerAssetMetaCategory
                {
                    Id = m.Id,
                    Code = m.Code,
                    DisplayName = m.DisplayName,
                    IconPath = m.IconPath,
                    SortOrder = m.SortOrder,
                    Categories =
                    [
                        .. m.Categories.Select(c => new DTPlayerAssetCategory
                        {
                            Id = c.Id,
                            Code = c.Code,
                            DisplayName = c.DisplayName,
                            SubjectPlayerId = c.SubjectPlayerId,
                            CategoryType = c.CategoryType,
                            SortOrder = c.SortOrder,
                            Groups =
                            [
                                .. c.Groups.Select(g => new DTPlayerAssetGroup
                                {
                                    Id = g.Id,
                                    Code = g.Code,
                                    DisplayName = g.DisplayName,
                                    SortOrder = g.SortOrder,
                                    Assets = [.. g.Assets.Select(ToWireAsset)]
                                })
                            ],
                            Assets = [.. c.Assets.Select(ToWireAsset)]
                        })
                    ]
                })
            ]
        });
    }

    /// <summary>
    /// Transcribes one Asset onto the wire.
    /// </summary>
    /// <remarks>
    /// Shared by the grouped and standalone arms of the tree, which carry the same Asset shape by two different paths.
    /// </remarks>
    private static DTPlayerAsset ToWireAsset(PlayerAsset asset)
    {
        return new DTPlayerAsset
        {
            Id = asset.Id,
            Code = asset.Code,
            DisplayName = asset.DisplayName,
            Description = asset.Description,
            IconPath = asset.IconPath,
            IsGradedAsset = asset.IsGradedAsset,
            AssetGrade = asset.AssetGrade,
            SortOrder = asset.SortOrder,
            NeverLuxury = asset.NeverLuxury,
            ObsoleteInRounds = asset.ObsoleteInRounds,
            Amount = asset.Amount,
            AmountAddedPerTurn = asset.AmountAddedPerTurn,
            HasStrategicStockpile = asset.HasStrategicStockpile,
            IsImported = asset.IsImported,
            IsExported = asset.IsExported,
            DemandNotMet = asset.DemandNotMet,
            IsLuxury = asset.IsLuxury
        };
    }
}
