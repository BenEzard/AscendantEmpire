using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Resources.Services;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.BackendContracts.Resources.GetPlayerResources;
using AscendantEmpire.Domain.Enums.Resources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Resources.Handlers;

/// <summary>
/// Reads the resource tree a Player can currently see: their unlocked meta-categories, and everything visible beneath them.
/// </summary>
/// <param name="GameId">The Game the Player belongs to, whose current Round the obsoleting countdowns are measured against.</param>
public record GetPlayerResourcesRequest(int GameId, int PlayerId) : IRequest<ServiceResult<IReadOnlyCollection<PlayerResourceMetaCategory>>>;

/// <remarks>
/// Registered and run by <c>ValidationBehaviour</c> before the handler is reached, so the handler does not invoke it itself.
/// </remarks>
internal class GetPlayerResourcesRequestValidator : Validator<GetPlayerResourcesRequest>
{
    public GetPlayerResourcesRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

/// <summary>
/// Builds the Resources toolbar's whole tree for one Player in one request.
/// </summary>
/// <remarks>
/// <para>
/// One endpoint rather than several: the toolbar needs the unlocked meta-category list before any panel is opened, and a category's or group's
/// visibility is a function of the Resources beneath it, so splitting the call would force the client to recombine what the server already knows. The
/// tree is a few hundred rows. If per-Round refetch cost ever matters, the split to reach for is a slim catalogue call plus a quantities call, not a
/// call per meta-category.
/// </para>
/// <para>
/// Four flat reads assembled in memory, rather than one query with nested projections. Two reasons. The Resource read is driven from the unlock table
/// so that <c>ObsoleteOnRound</c> is a column on the same row the availability predicate filtered, which is what stops the countdown and the
/// availability check drifting apart; and the meta-category read consults nothing beneath it, so "an unlocked meta-category with nothing visible
/// under it still appears" is structural rather than a rule a later edit could quietly break.
/// </para>
/// </remarks>
internal class GetPlayerResourcesHandler(
    ILogger<GetPlayerResourcesHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetPlayerResourcesRequest, ServiceResult<IReadOnlyCollection<PlayerResourceMetaCategory>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<PlayerResourceMetaCategory>>> Handle(
        GetPlayerResourcesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // A cheap "is this Player part of this in-progress Game" probe, so a request cannot be answered out of whatever unlock rows its Player id
            // happens to have.
            ServiceResult<IReadOnlyCollection<GameContext>> gameContextResult =
                await mediator.Send(new GetGameContextRequest(request.GameId, request.PlayerId), cancellationToken);

            if (gameContextResult.SuccessValue is not { Count: > 0 })
            {
                string error = $"No in-progress game found for Game Id: {request.GameId} and Player Id: {request.PlayerId}.";
                logger.LogWarning("{Error}", error);

                return ServiceResult<IReadOnlyCollection<PlayerResourceMetaCategory>>.NotFound(error);
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            ServiceResult<int> currentRoundResult = await PlayerResourceUnlockLookup.ReadCurrentRoundAsync(context, request.GameId, cancellationToken);

            if (currentRoundResult.HasSucceeded == false)
            {
                return ServiceResult<IReadOnlyCollection<PlayerResourceMetaCategory>>.NotFound(currentRoundResult.Errors);
            }

            IReadOnlyCollection<PlayerResourceMetaCategory> tree = await ReadTreeAsync(
                context,
                request.PlayerId,
                currentRoundResult.SuccessValue,
                cancellationToken);

            return ServiceResult<IReadOnlyCollection<PlayerResourceMetaCategory>>.Succeed(tree);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(GetPlayerResourcesRequest));

            return ServiceResult<IReadOnlyCollection<PlayerResourceMetaCategory>>.Fail("An unexpected error occurred while retrieving player resources.");
        }
    }

    /// <summary>
    /// Reads the four row sets the tree is built from, then assembles them.
    /// </summary>
    private static async Task<IReadOnlyCollection<PlayerResourceMetaCategory>> ReadTreeAsync(
        AscendantEmpireContext context,
        int playerId,
        int currentRound,
        CancellationToken cancellationToken)
    {
        // The meta-categories the Player has unlocked and has not yet lost. Nothing beneath is consulted: an unlocked meta-category with nothing
        // available under it still shows its icon over an empty panel - see PlayerUnlockedResourceMetaCategory's remarks.
        List<UnlockedMetaCategoryRow> metaCategoryRows = await (
            from unlock in context.PlayerUnlockedResourceMetaCategories.AsNoTracking()
            join metaCategory in context.ResourceMetaCategories.AsNoTracking() on unlock.ResourceMetaCategoryId equals metaCategory.Id
            where unlock.PlayerId == playerId
                && (unlock.ObsoleteOnRound == null || unlock.ObsoleteOnRound > currentRound)
            select new UnlockedMetaCategoryRow(
                metaCategory.Id,
                metaCategory.Code,
                metaCategory.DisplayName,
                metaCategory.IconPath,
                metaCategory.SortOrder))
            .ToListAsync(cancellationToken);

        if (metaCategoryRows.Count == 0)
        {
            return [];
        }

        // Driven from the unlock table rather than the catalogue, so ObsoleteOnRound is a column on the same row the predicate filtered.
        //
        // Deliberately not narrowed by Resource.ResourceMetaCategoryId: that column is denormalised and nothing enforces that it agrees with the
        // category chain, so filtering on it would let a mis-seeded Resource pass the meta-category gate and then be placed under a category belonging
        // to a different meta-category. Placement below goes through ResourceGroupId / ResourceCategoryId alone, which leaves the catalogue's
        // exclusive-or chain as the single source of truth for both gating and placement.
        List<UnlockedResourceRow> resourceRows = await (
            from unlock in context.PlayerUnlockedResources.AsNoTracking()
            join resource in context.Resources.AsNoTracking() on unlock.ResourceId equals resource.Id
            where unlock.PlayerId == playerId
                && (unlock.ObsoleteOnRound == null || unlock.ObsoleteOnRound > currentRound)
            select new UnlockedResourceRow(
                resource.Id,
                resource.Code,
                resource.DisplayName,
                resource.Description,
                resource.IconPath,
                resource.IsGradedResource,
                resource.ResourceGrade,
                resource.SortOrder,
                resource.NeverLuxury,
                resource.ResourceGroupId,
                resource.ResourceCategoryId,
                unlock.ObsoleteOnRound))
            .ToListAsync(cancellationToken);

        // The catalogue scaffolding between a Resource and its meta-category. Neither level has unlock rows of its own, so both are read whole for the
        // visible meta-categories and then pruned by whatever actually lands in them.
        int[] metaCategoryIds = [.. metaCategoryRows.Select(metaCategory => metaCategory.Id)];

        List<CatalogueCategoryRow> categoryRows = await context.ResourceCategories
            .AsNoTracking()
            .Where(category => metaCategoryIds.Contains(category.ResourceMetaCategoryId))
            .Select(category => new CatalogueCategoryRow(
                category.Id,
                category.ResourceMetaCategoryId,
                category.Code,
                category.DisplayName,
                category.CategoryType,
                category.SortOrder))
            .ToListAsync(cancellationToken);

        int[] categoryIds = [.. categoryRows.Select(category => category.Id)];

        List<CatalogueGroupRow> groupRows = await context.ResourceGroups
            .AsNoTracking()
            .Where(group => categoryIds.Contains(group.ResourceCategoryId))
            .Select(group => new CatalogueGroupRow(group.Id, group.ResourceCategoryId, group.Code, group.DisplayName, group.SortOrder))
            .ToListAsync(cancellationToken);

        return BuildTree(metaCategoryRows, categoryRows, groupRows, resourceRows, currentRound);
    }

    /// <summary>
    /// Assembles the four row sets into the nested tree, dropping every group and category that nothing visible landed in.
    /// </summary>
    private static IReadOnlyCollection<PlayerResourceMetaCategory> BuildTree(
        IReadOnlyCollection<UnlockedMetaCategoryRow> metaCategoryRows,
        IReadOnlyCollection<CatalogueCategoryRow> categoryRows,
        IReadOnlyCollection<CatalogueGroupRow> groupRows,
        IReadOnlyCollection<UnlockedResourceRow> resourceRows,
        int currentRound)
    {
        ILookup<int, PlayerResource> resourcesByGroupId = resourceRows
            .Where(resource => resource.ResourceGroupId is not null)
            .ToLookup(resource => resource.ResourceGroupId!.Value, resource => ToContract(resource, currentRound));

        ILookup<int, PlayerResource> resourcesByCategoryId = resourceRows
            .Where(resource => resource.ResourceCategoryId is not null)
            .ToLookup(resource => resource.ResourceCategoryId!.Value, resource => ToContract(resource, currentRound));

        ILookup<int, CatalogueGroupRow> groupsByCategoryId = groupRows.ToLookup(group => group.ResourceCategoryId);
        ILookup<int, CatalogueCategoryRow> categoriesByMetaCategoryId = categoryRows.ToLookup(category => category.ResourceMetaCategoryId);

        return
        [
            .. metaCategoryRows
                .OrderBy(metaCategory => metaCategory.SortOrder)
                .ThenBy(metaCategory => metaCategory.DisplayName)
                .Select(metaCategory => new PlayerResourceMetaCategory
                {
                    Id = metaCategory.Id,
                    Code = metaCategory.Code,
                    DisplayName = metaCategory.DisplayName,
                    IconPath = metaCategory.IconPath,
                    SortOrder = metaCategory.SortOrder,
                    Categories = BuildCategories(
                        categoriesByMetaCategoryId[metaCategory.Id],
                        groupsByCategoryId,
                        resourcesByGroupId,
                        resourcesByCategoryId)
                })
        ];
    }

    /// <summary>
    /// Builds one meta-category's visible categories, in display order.
    /// </summary>
    private static IReadOnlyCollection<PlayerResourceCategory> BuildCategories(
        IEnumerable<CatalogueCategoryRow> categoryRows,
        ILookup<int, CatalogueGroupRow> groupsByCategoryId,
        ILookup<int, PlayerResource> resourcesByGroupId,
        ILookup<int, PlayerResource> resourcesByCategoryId)
    {
        List<PlayerResourceCategory> categories = [];

        foreach (CatalogueCategoryRow row in categoryRows.OrderBy(category => category.SortOrder).ThenBy(category => category.DisplayName))
        {
            IReadOnlyCollection<PlayerResourceGroup> groups = row.CategoryType == ResourceCategoryType.GroupedResources
                ? BuildGroups(groupsByCategoryId[row.Id], resourcesByGroupId)
                : [];

            IReadOnlyCollection<PlayerResource> resources = row.CategoryType == ResourceCategoryType.StandaloneResources
                ? OrderResources(resourcesByCategoryId[row.Id])
                : [];

            // A category has no unlock row of its own. It is here because something beneath it is, and it goes with the last of them.
            if (groups.Count == 0 && resources.Count == 0)
            {
                continue;
            }

            categories.Add(new PlayerResourceCategory
            {
                Id = row.Id,
                Code = row.Code,
                DisplayName = row.DisplayName,
                CategoryType = row.CategoryType,
                SortOrder = row.SortOrder,
                Groups = groups,
                Resources = resources
            });
        }

        return categories;
    }

    /// <summary>
    /// Builds one grouped category's visible groups, in display order.
    /// </summary>
    private static IReadOnlyCollection<PlayerResourceGroup> BuildGroups(
        IEnumerable<CatalogueGroupRow> groupRows,
        ILookup<int, PlayerResource> resourcesByGroupId)
    {
        List<PlayerResourceGroup> groups = [];

        // An unnumbered group sorts after every numbered one and then by name. Spelled out rather than left to OrderBy, which puts a null int? first -
        // a group nobody has given a position should not jump to the top of the panel.
        foreach (CatalogueGroupRow row in groupRows.OrderBy(group => group.SortOrder ?? int.MaxValue).ThenBy(group => group.DisplayName))
        {
            IReadOnlyCollection<PlayerResource> resources = OrderResources(resourcesByGroupId[row.Id]);

            if (resources.Count == 0)
            {
                continue;
            }

            groups.Add(new PlayerResourceGroup
            {
                Id = row.Id,
                Code = row.Code,
                DisplayName = row.DisplayName,
                SortOrder = row.SortOrder,
                Resources = resources
            });
        }

        return groups;
    }

    /// <summary>
    /// Orders the Resources within one group or standalone category.
    /// </summary>
    /// <remarks>
    /// Grade breaks the final tie: every grade of a graded Resource shares one display name, so all of them would otherwise come back in whatever order
    /// the lookup happened to hold them.
    /// </remarks>
    private static IReadOnlyCollection<PlayerResource> OrderResources(IEnumerable<PlayerResource> resources)
    {
        return [.. resources.OrderBy(r => r.SortOrder).ThenBy(r => r.DisplayName).ThenBy(r => r.ResourceGrade)];
    }

    private static PlayerResource ToContract(UnlockedResourceRow row, int currentRound)
    {
        return new PlayerResource
        {
            Id = row.Id,
            Code = row.Code,
            DisplayName = row.DisplayName,
            Description = row.Description,
            IconPath = row.IconPath,
            IsGradedResource = row.IsGradedResource,
            ResourceGrade = row.ResourceGrade,
            SortOrder = row.SortOrder,
            NeverLuxury = row.NeverLuxury,

            // ObsoleteOnRound is future-dated, and only a row still ahead of the current Round reaches here, so this is always positive.
            ObsoleteInRounds = row.ObsoleteOnRound is null ? null : row.ObsoleteOnRound - currentRound,

            // Placeholders - nothing sources these yet. See the banner on the PlayerResource contract.
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
        int ResourceMetaCategoryId,
        string Code,
        string DisplayName,
        ResourceCategoryType CategoryType,
        int SortOrder);

    private sealed record CatalogueGroupRow(int Id, int ResourceCategoryId, string Code, string DisplayName, int? SortOrder);

    private sealed record UnlockedResourceRow(
        int Id,
        string Code,
        string DisplayName,
        string? Description,
        string IconPath,
        bool IsGradedResource,
        ResourceGrade ResourceGrade,
        int SortOrder,
        bool NeverLuxury,
        int? ResourceGroupId,
        int? ResourceCategoryId,
        int? ObsoleteOnRound);
}
