using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Players.Classes;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Players.GetPlayers;
using AscendantEmpire.BackendContracts.Players.GetPlayerStructures;
using AscendantEmpire.BackendContracts.Assets.GetAssetBanks;
using AscendantEmpire.BackendContracts.Assets.GetPlayerAssets;
using AscendantEmpire.BackendContracts.Units.GetPlayerForces;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Player;
using AscendantEmpire.FrontendContracts.Assets.GetAssetBanks;
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
                            CategoryType = c.CategoryType,
                            SortOrder = c.SortOrder,
                            Groups =
                            [
                                .. c.Groups.Select(g => new DTPlayerAssetGroup
                                {
                                    Id = g.Id,
                                    Code = g.Code,
                                    DisplayName = g.DisplayName,
                                    SubjectPlayerId = g.SubjectPlayerId,
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

    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // Asset banks
    //
    // What the Player actually holds, as opposed to which Assets they can see. A bank is one Settlement's own store, so these reads are keyed on a
    // Settlement or on the Player rather than on an Asset. See docs/design/asset-banks.md.
    //
    // Both shapes come back with each bank's Settlement and NetworkKey alongside its balances: a quantity on its own invites a caller to add two banks
    // together whose Settlements cannot reach each other.
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Get every asset bank the Player owns, which together are everything they hold anywhere
    /// </summary>
    /// <remarks>
    /// A bank per Settlement rather than one merged set of totals, with each bank's <c>networkKey</c> saying which of them can reach each other. 
    /// A single figure would advertise stock that no one build order could spend; a caller that wants one number adds them up knowing that. 
    /// A Player with no settlement yet owns no banks, which is an empty list rather than a 404.
    /// </remarks>
    /// <response code="200">Every bank the Player owns, each with its Settlement, network key and holdings</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">The Player is not in an in-progress Game</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("asset-banks")]
    [ProducesResponseType(typeof(IAssetBankCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerAssetBanksAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        // Game and player come from the token, so the caller can't ask for someone else's data.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<IReadOnlyCollection<AssetBankHoldings>> response =
            await mediator.Send(new GetPlayerAssetBanksRequest(gameId, playerId), cancelToken);

        return response.ToActionResult(banks => new DTAssetBankCollection
        {
            Banks = [.. (banks ?? []).Select(ToWireAssetBank)]
        });
    }

    /// <summary>
    /// Get every asset bank one of the Player's Settlements can draw on
    /// </summary>
    /// <remarks>
    /// Keyed on the Settlement rather than on the bank: a Settlement's own bank never changes, but which network it sits in moves as roads are built and
    /// pillaged, so the set of banks returned is not something a client can cache. A Settlement with no bank and a Settlement belonging to another Player are
    /// both reported as 404.
    /// </remarks>
    /// <response code="200">The network, with the site's own bank named and every bank's holdings</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">The Player is not in an in-progress Game, or the Settlement has no asset bank belonging to them</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("asset-banks/{settlementId:int}")]
    [ProducesResponseType(typeof(IAssetNetworkHoldings), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetSettlementAssetNetworkAsync(
        [FromServices] IMediator mediator,
        int settlementId,
        CancellationToken cancelToken)
    {
        // Game and player come from the token, so the caller can't ask for someone else's data.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<AssetNetworkHoldings> response =
            await mediator.Send(new GetSettlementAssetNetworkRequest(gameId, playerId, settlementId), cancelToken);

        return response.ToActionResult(ToWireAssetNetwork);
    }

    /// <summary>
    /// Builds the response for one Settlement's network: the key the banks share, which of them is the site, and each bank's holdings.
    /// </summary>
    /// <remarks>
    /// <see cref="AssetNetworkHoldings.PlayerId"/> is deliberately not carried. Game and Player both come off the caller's token, so echoing the Player back
    /// tells a client only what it supplied, and no other response in this controller carries it either.
    /// </remarks>
    private static DTAssetNetworkHoldings ToWireAssetNetwork(AssetNetworkHoldings network)
    {
        return new DTAssetNetworkHoldings
        {
            NetworkKey = network.NetworkKey,
            SiteAssetBankId = network.SiteAssetBankId,
            Banks = [.. network.Banks.Select(ToWireAssetBank)]
        };
    }

    /// <summary>
    /// Transcribes one asset bank onto the wire, turning its scaled balances into the whole numbers the UI speaks.
    /// </summary>
    /// <remarks>
    /// Rounded down, so a bank holding nine tenths of a log reads as none of it - which is also all the drawdown would let the Player spend of it.
    /// </remarks>
    private static DTAssetBankHoldings ToWireAssetBank(AssetBankHoldings bank)
    {
        return new DTAssetBankHoldings
        {
            AssetBankId = bank.AssetBankId,
            SettlementId = bank.SettlementId,
            NetworkKey = bank.NetworkKey,
            Holdings =
            [
                .. bank.Holdings.Select(holding => new DTAssetBankHolding
                {
                    AssetId = holding.AssetId,
                    AssetCode = holding.AssetCode,
                    DisplayName = holding.DisplayName,
                    AssetGroupId = holding.AssetGroupId,
                    Quantity = ScaledAmount.FromRaw(holding.RawQuantity).ToWholeRoundedDown()
                })
            ]
        };
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
