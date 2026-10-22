using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.BackendContracts.Assets.GetPlayerAssets;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Handlers;

/// <summary>
/// Reads the asset tree a Player can currently see: their unlocked meta-categories, and everything visible beneath them.
/// </summary>
/// <param name="GameId">The Game the Player belongs to, whose current Round the obsoleting countdowns are measured against.</param>
public record GetPlayerAssetsRequest(int GameId, int PlayerId) : IRequest<ServiceResult<IReadOnlyCollection<PlayerAssetMetaCategory>>>;

/// <remarks>
/// Registered and run by <c>ValidationBehaviour</c> before the handler is reached, so the handler does not invoke it itself.
/// </remarks>
internal class GetPlayerAssetsRequestValidator : Validator<GetPlayerAssetsRequest>
{
    public GetPlayerAssetsRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

/// <summary>
/// Builds the Assets toolbar's whole tree for one Player in one request.
/// </summary>
/// <remarks>
/// <para>
/// One endpoint rather than several: the toolbar needs the unlocked meta-category list before any panel is opened, and a category's or group's
/// visibility is a function of the Assets beneath it, so splitting the call would force the client to recombine what the server already knows. The
/// tree is a few hundred rows. If per-Round refetch cost ever matters, the split to reach for is a slim catalogue call plus a quantities call, not a
/// call per meta-category.
/// </para>
/// <para>
/// Five flat reads assembled in memory, rather than one query with nested projections. Two reasons. The Asset read is driven from the unlock table
/// so that <c>ObsoleteOnRound</c> is a column on the same row the availability predicate filtered, which is what stops the countdown and the
/// availability check drifting apart; and the meta-category read consults nothing beneath it, so "an unlocked meta-category with nothing visible
/// under it still appears" is structural rather than a rule a later edit could quietly break.
/// </para>
/// </remarks>
internal class GetPlayerAssetsHandler(
    ILogger<GetPlayerAssetsHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetPlayerAssetsRequest, ServiceResult<IReadOnlyCollection<PlayerAssetMetaCategory>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<PlayerAssetMetaCategory>>> Handle(
        GetPlayerAssetsRequest request,
        CancellationToken cancellationToken)
    {
        // A cheap "is this Player part of this in-progress Game" probe, so a request cannot be answered out of whatever unlock rows its Player id
        // happens to have.
        ServiceResult<IReadOnlyCollection<GameContext>> gameContextResult =
            await mediator.Send(new GetGameContextRequest(request.GameId, request.PlayerId), cancellationToken);

        if (gameContextResult.SuccessValue is not { Count: > 0 })
        {
            string error = $"No in-progress game found for Game Id: {request.GameId} and Player Id: {request.PlayerId}.";
            logger.LogWarning("{Error}", error);

            return ServiceResult<IReadOnlyCollection<PlayerAssetMetaCategory>>.NotFound(error);
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

        ServiceResult<int> currentRoundResult = await PlayerAssetUnlockLookup.ReadCurrentRoundAsync(context, request.GameId, cancellationToken);

        if (currentRoundResult.HasSucceeded == false)
        {
            return ServiceResult<IReadOnlyCollection<PlayerAssetMetaCategory>>.NotFound(currentRoundResult.Errors);
        }

        IReadOnlyCollection<PlayerAssetMetaCategory> tree = await ReadTreeAsync(
            context,
            request.GameId,
            request.PlayerId,
            currentRoundResult.SuccessValue,
            cancellationToken);

        return ServiceResult<IReadOnlyCollection<PlayerAssetMetaCategory>>.Succeed(tree);
    }

    /// <summary>
    /// Reads the five row sets the tree is built from, then assembles them.
    /// </summary>
    private static async Task<IReadOnlyCollection<PlayerAssetMetaCategory>> ReadTreeAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        int currentRound,
        CancellationToken cancellationToken)
    {
        // The meta-categories the Player has unlocked and has not yet lost.
        // Nothing beneath is consulted: an unlocked meta-category with nothing available under it still shows its icon over an empty panel - see PlayerUnlockedAssetMetaCategory's remarks.
        List<UnlockedMetaCategoryRow> metaCategoryRows = await context.PlayerUnlockedAssetMetaCategories
            .AsNoTracking()
            .Where(unlock => unlock.PlayerId == playerId)
            .Where(unlock => unlock.ObsoleteOnRound == null || unlock.ObsoleteOnRound > currentRound)
            .Select(unlock => new UnlockedMetaCategoryRow(
                unlock.MetaCategory.Id,
                unlock.MetaCategory.Code,
                unlock.MetaCategory.DisplayName,
                unlock.MetaCategory.IconPath,
                unlock.MetaCategory.SortOrder))
            .ToListAsync(cancellationToken);

        if (metaCategoryRows.Count == 0)
        {
            return [];
        }

        // Driven from the unlock table rather than the catalogue, so ObsoleteOnRound is a column on the same row the predicate filtered.
        //
        // Deliberately not narrowed by Asset.AssetMetaCategoryId: that column is denormalised and nothing enforces that it agrees with the
        // category chain, so filtering on it would let a mis-seeded Asset pass the meta-category gate and then be placed under a category belonging
        // to a different meta-category. Placement below goes through AssetGroupId / AssetCategoryId alone, which leaves the catalogue's
        // exclusive-or chain as the single source of truth for both gating and placement.
        List<UnlockedAssetRow> assetRows = await context.PlayerUnlockedAssets
            .AsNoTracking()
            .Where(unlock => unlock.PlayerId == playerId)
            .Where(unlock => unlock.ObsoleteOnRound == null || unlock.ObsoleteOnRound > currentRound)
            .Select(unlock => new UnlockedAssetRow(
                unlock.Asset.Id,
                unlock.Asset.Code,
                unlock.Asset.DisplayName,
                unlock.Asset.Description,
                unlock.Asset.IconPath,
                unlock.Asset.IsGradedAsset,
                unlock.Asset.AssetGrade,
                unlock.Asset.SortOrder,
                unlock.Asset.NeverLuxury,
                unlock.Asset.AssetGroupId,
                unlock.Asset.AssetCategoryId,
                unlock.ObsoleteOnRound))
            .ToListAsync(cancellationToken);

        // The catalogue scaffolding between an Asset and its meta-category. Neither level has unlock rows of its own, so both are read whole for the
        // visible meta-categories and then pruned by whatever actually lands in them.
        int[] metaCategoryIds = [.. metaCategoryRows.Select(metaCategory => metaCategory.Id)];

        List<CatalogueCategoryRow> categoryRows = await context.AssetCategories
            .AsNoTracking()
            .Where(category => metaCategoryIds.Contains(category.AssetMetaCategoryId))
            .Select(category => new CatalogueCategoryRow(
                category.Id,
                category.AssetMetaCategoryId,
                category.Code,
                category.DisplayName,
                category.CategoryType,
                category.SubjectScope,
                category.SortOrder))
            .ToListAsync(cancellationToken);

        int[] categoryIds = [.. categoryRows.Select(category => category.Id)];

        List<CatalogueGroupRow> groupRows = await context.AssetGroups
            .AsNoTracking()
            .Where(group => categoryIds.Contains(group.AssetCategoryId))
            .Select(group => new CatalogueGroupRow(group.Id, group.AssetCategoryId, group.Code, group.DisplayName, group.SortOrder))
            .ToListAsync(cancellationToken);

        // Only read when the catalogue actually holds a template, so an ordinary game pays nothing for a feature it is not using.
        IReadOnlyCollection<SubjectPlayerRow> subjectPlayers =
            categoryRows.Any(category => category.SubjectScope == AssetSubjectScope.PerOtherPlayer)
                ? await ReadSubjectPlayersAsync(context, gameId, playerId, cancellationToken)
                : [];

        return BuildTree(metaCategoryRows, categoryRows, groupRows, assetRows, subjectPlayers, currentRound);
    }

    /// <summary>
    /// The other Players in the game whose held populations this Player gets a category for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both of the gates this is supposed to apply are placeholders.</b> RS-20 calls for a category per <i>discovered</i> Player, and only where
    /// the figures beneath it are above zero. Neither can be honoured yet:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Discovery.</b> There is no record of one Player having met another. <c>PlayerKnownForce</c> and <c>PlayerKnownStructure</c> carry an
    /// observer and an owner, so "have I ever seen anything of theirs" is derivable, but a derived answer forgets if sighting rows are ever
    /// pruned, and first contact is a Diplomacy concern rather than something to invent here. Every other Player is returned until that lands.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>A figure above zero.</b> There is no holdings table - <c>Amount</c> is a placeholder the handler writes as zero for every Asset in the
    /// catalogue. Once the ledger exists the gate is free, because no row will mean no category; until then every qualifying Player is returned and
    /// their panel reads zero, which is how the rest of the toolbar already behaves.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// <b>Status is deliberately not filtered on, eliminated Players included.</b> A people outlives the polity that spoke for it.
    /// </para>
    /// <para>
    /// Once the ledger exists the rule becomes "hold rows against them, or they are a Player I have discovered". Holdings are what keep a category
    /// alive past elimination, and an eliminated Player nobody holds anything of then drops out through the above-zero gate rather than by status.
    /// </para>
    /// </remarks>
    private static async Task<IReadOnlyCollection<SubjectPlayerRow>> ReadSubjectPlayersAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        CancellationToken cancellationToken)
    {
        return await context.Players
            .AsNoTracking()
            .Where(player => player.GameId == gameId)
            .Where(player => player.Id != playerId)
            .Select(player => new SubjectPlayerRow(player.Id, player.PeopleName))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Assembles the five row sets into the nested tree, dropping every group and category that nothing visible landed in.
    /// </summary>
    private static IReadOnlyCollection<PlayerAssetMetaCategory> BuildTree(
        IReadOnlyCollection<UnlockedMetaCategoryRow> metaCategoryRows,
        IReadOnlyCollection<CatalogueCategoryRow> categoryRows,
        IReadOnlyCollection<CatalogueGroupRow> groupRows,
        IReadOnlyCollection<UnlockedAssetRow> assetRows,
        IReadOnlyCollection<SubjectPlayerRow> subjectPlayers,
        int currentRound)
    {
        ILookup<int, PlayerAsset> assetsByGroupId = assetRows
            .Where(asset => asset.AssetGroupId is not null)
            .ToLookup(asset => asset.AssetGroupId!.Value, asset => ToContract(asset, currentRound));

        ILookup<int, PlayerAsset> assetsByCategoryId = assetRows
            .Where(asset => asset.AssetCategoryId is not null)
            .ToLookup(asset => asset.AssetCategoryId!.Value, asset => ToContract(asset, currentRound));

        ILookup<int, CatalogueGroupRow> groupsByCategoryId = groupRows.ToLookup(group => group.AssetCategoryId);
        ILookup<int, CatalogueCategoryRow> categoriesByMetaCategoryId = categoryRows.ToLookup(category => category.AssetMetaCategoryId);

        return
        [
            .. metaCategoryRows
                .OrderBy(metaCategory => metaCategory.SortOrder)
                .ThenBy(metaCategory => metaCategory.DisplayName)
                .Select(metaCategory => new PlayerAssetMetaCategory
                {
                    Id = metaCategory.Id,
                    Code = metaCategory.Code,
                    DisplayName = metaCategory.DisplayName,
                    IconPath = metaCategory.IconPath,
                    SortOrder = metaCategory.SortOrder,
                    Categories = BuildCategories(
                        categoriesByMetaCategoryId[metaCategory.Id],
                        groupsByCategoryId,
                        assetsByGroupId,
                        assetsByCategoryId,
                        subjectPlayers)
                })
        ];
    }

    /// <summary>
    /// Builds one meta-category's visible categories, in display order.
    /// </summary>
    /// <remarks>
    /// A category marked <see cref="AssetSubjectScope.PerOtherPlayer"/> is emitted once, under its own name. What is stamped out per subject Player
    /// is each of its groups - see <see cref="BuildGroups"/> - so the whole fan-out is already done by the time the emptiness check below runs.
    /// </remarks>
    private static IReadOnlyCollection<PlayerAssetCategory> BuildCategories(
        IEnumerable<CatalogueCategoryRow> categoryRows,
        ILookup<int, CatalogueGroupRow> groupsByCategoryId,
        ILookup<int, PlayerAsset> assetsByGroupId,
        ILookup<int, PlayerAsset> assetsByCategoryId,
        IReadOnlyCollection<SubjectPlayerRow> subjectPlayers)
    {
        List<PlayerAssetCategory> categories = [];

        foreach (CatalogueCategoryRow row in categoryRows.OrderBy(category => category.SortOrder).ThenBy(category => category.DisplayName))
        {
            IReadOnlyCollection<PlayerAssetGroup> groups = row.CategoryType == AssetCategoryType.GroupedAssets
                ? BuildGroups(groupsByCategoryId[row.Id], assetsByGroupId, row.SubjectScope, subjectPlayers)
                : [];

            IReadOnlyCollection<PlayerAsset> assets = row.CategoryType == AssetCategoryType.StandaloneAssets
                ? OrderAssets(assetsByCategoryId[row.Id])
                : [];

            // A category has no unlock row of its own. It is here because something beneath it is, and it goes with the last of them. On a
            // per-other-player category this covers both ways its groups can come back empty - nothing beneath the templates is unlocked, or there
            // is no other Player to stamp them onto - so the Player sees no "Enemies" category at all rather than an empty one.
            if (groups.Count == 0 && assets.Count == 0)
            {
                continue;
            }

            categories.Add(new PlayerAssetCategory
            {
                Id = row.Id,
                Code = row.Code,
                DisplayName = row.DisplayName,
                CategoryType = row.CategoryType,
                SortOrder = row.SortOrder,
                Groups = groups,
                Assets = assets
            });
        }

        return categories;
    }

    /// <summary>
    /// Builds one grouped category's visible groups, in display order.
    /// </summary>
    /// <remarks>
    /// Beneath a category marked <see cref="AssetSubjectScope.PerOtherPlayer"/> every group is a <b>template</b> and is never emitted as itself. It
    /// is stamped out once per entry in <paramref name="subjectPlayers"/> instead, each copy carrying the template's Assets under that Player's
    /// people name - which is what lets the catalogue stay global while the panel is per-game. With no subject Players the templates yield nothing
    /// and the caller drops the emptied category.
    /// </remarks>
    /// <param name="subjectScope">The owning category's scope, which is what decides whether these groups are templates or ordinary groups.</param>
    /// <param name="subjectPlayers">Ignored unless <paramref name="subjectScope"/> is per-other-player.</param>
    private static IReadOnlyCollection<PlayerAssetGroup> BuildGroups(
        IEnumerable<CatalogueGroupRow> groupRows,
        ILookup<int, PlayerAsset> assetsByGroupId,
        AssetSubjectScope subjectScope,
        IReadOnlyCollection<SubjectPlayerRow> subjectPlayers)
    {
        List<PlayerAssetGroup> groups = [];

        // An unnumbered group sorts after every numbered one and then by name. Spelled out rather than left to OrderBy, which puts a null int? first -
        // a group nobody has given a position should not jump to the top of the panel.
        foreach (CatalogueGroupRow row in groupRows.OrderBy(group => group.SortOrder ?? int.MaxValue).ThenBy(group => group.DisplayName))
        {
            IReadOnlyCollection<PlayerAsset> assets = OrderAssets(assetsByGroupId[row.Id]);

            // Checked before the fan-out below, so a Player who has not unlocked the foreign population Assets gets nothing here rather than one
            // empty column per opponent.
            if (assets.Count == 0)
            {
                continue;
            }

            if (subjectScope == AssetSubjectScope.PerOtherPlayer)
            {
                // Ordered by people name so the columns read alphabetically. The names are unique within a game and locked once play begins (GS-11),
                // so this is a stable order rather than one that shuffles between reads. The fan-out is nested inside the template loop rather than
                // outside it, which keeps one template's opponents together instead of interleaving them with the next template's.
                foreach (SubjectPlayerRow subject in subjectPlayers.OrderBy(player => player.PeopleName).ThenBy(player => player.PlayerId))
                {
                    groups.Add(new PlayerAssetGroup
                    {
                        // The template's own id, repeated across its projections: a projected group has no row of its own. Code is what identifies
                        // one - see the remarks on the contract.
                        Id = row.Id,
                        Code = $"{row.Code}.P{subject.PlayerId}",
                        DisplayName = subject.PeopleName,
                        SubjectPlayerId = subject.PlayerId,
                        SortOrder = row.SortOrder,
                        Assets = assets
                    });
                }

                continue;
            }

            groups.Add(new PlayerAssetGroup
            {
                Id = row.Id,
                Code = row.Code,
                DisplayName = row.DisplayName,
                SubjectPlayerId = null,
                SortOrder = row.SortOrder,
                Assets = assets
            });
        }

        return groups;
    }

    /// <summary>
    /// Orders the Assets within one group or standalone category.
    /// </summary>
    /// <remarks>
    /// Grade breaks the final tie: every grade of a graded Asset shares one display name, so all of them would otherwise come back in whatever order
    /// the lookup happened to hold them.
    /// </remarks>
    private static IReadOnlyCollection<PlayerAsset> OrderAssets(IEnumerable<PlayerAsset> assets)
    {
        return [.. assets.OrderBy(r => r.SortOrder).ThenBy(r => r.DisplayName).ThenBy(r => r.AssetGrade)];
    }

    private static PlayerAsset ToContract(UnlockedAssetRow row, int currentRound)
    {
        return new PlayerAsset
        {
            Id = row.Id,
            Code = row.Code,
            DisplayName = row.DisplayName,
            Description = row.Description,
            IconPath = row.IconPath,
            IsGradedAsset = row.IsGradedAsset,
            AssetGrade = row.AssetGrade,
            SortOrder = row.SortOrder,
            NeverLuxury = row.NeverLuxury,

            // ObsoleteOnRound is future-dated, and only a row still ahead of the current Round reaches here, so this is always positive.
            ObsoleteInRounds = row.ObsoleteOnRound is null ? null : row.ObsoleteOnRound - currentRound,

            // Placeholders - nothing sources these yet. See the banner on the PlayerAsset contract.
            Amount = 0,
            AmountAddedPerTurn = 0,
            HasStrategicStockpile = false,
            IsImported = false,
            IsExported = false,
            DemandNotMet = false,
            IsLuxury = false
        };
    }

    private sealed record UnlockedMetaCategoryRow(int Id, string Code, string DisplayName, string IconPath, int SortOrder);

    private sealed record CatalogueCategoryRow(
        int Id,
        int AssetMetaCategoryId,
        string Code,
        string DisplayName,
        AssetCategoryType CategoryType,
        AssetSubjectScope SubjectScope,
        int SortOrder);

    /// <summary>
    /// One other Player in the game, and the name their people are labelled with.
    /// </summary>
    private sealed record SubjectPlayerRow(int PlayerId, string PeopleName);

    private sealed record CatalogueGroupRow(int Id, int AssetCategoryId, string Code, string DisplayName, int? SortOrder);

    private sealed record UnlockedAssetRow(
        int Id,
        string Code,
        string DisplayName,
        string? Description,
        string IconPath,
        bool IsGradedAsset,
        AssetGrade AssetGrade,
        int SortOrder,
        bool NeverLuxury,
        int? AssetGroupId,
        int? AssetCategoryId,
        int? ObsoleteOnRound);
}
