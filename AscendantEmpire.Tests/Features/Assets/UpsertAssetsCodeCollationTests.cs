using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Pins the asset upsert against the collation its codes are actually stored under, which is the one thing about code matching an in-memory database cannot
/// reproduce.
/// </summary>
/// <remarks>
/// <para>
/// Containerised rather than in-memory, and the tier is forced by the thing under test rather than chosen: the unique indexes on <c>Code</c> are
/// case-insensitive because the database's collation is, while the EF in-memory provider compares strings ordinally. Under in-memory the bulk load would not
/// even return a differently-cased row, so the old ordinal lookups passed and the fixed ones pass for the wrong reason - the fault only exists where a real
/// engine enforces a real index.
/// </para>
/// <para>
/// What went wrong in the field, and what this stops coming back: the catalogue had <c>Society.Population.MyPeople.POW</c> stored, derived back when the
/// display name was "P.o.W", and the seed was changed to spell the code <c>...PoW</c>. SQL returned the stored row, the ordinal dictionary filed it under its
/// own casing, the lookup for the new casing missed, and the insert that followed was refused by the index the row was already in.
/// </para>
/// <para>
/// The companion in-memory fixture, <see cref="UpsertAssetsHandlerTests"/>, keeps everything the handler decides for itself - derivation, sort orders,
/// partial updates, and refusing two codes that collide. Only the collation belongs here.
/// </para>
/// </remarks>
[TestFixture]
internal class UpsertAssetsCodeCollationTests : SqlServerTestBase
{
    /// <summary>
    /// The codes the catalogue is first written with. Re-stated as constants so a test can assert the stored casing without restating the literal.
    /// </summary>
    private const string _META_CODE = "ConstructionMaterials";
    private const string _CATEGORY_CODE = "ConstructionMaterials.Wood";
    private const string _GROUP_CODE = "ConstructionMaterials.Wood.Logs";
    private const string _ASSET_CODE = "ConstructionMaterials.Wood.Logs.OakLog";

    /// <summary>
    /// How <see cref="BuildCatalogue"/> should case the codes it writes.
    /// </summary>
    /// <remarks>
    /// Which levels get re-cased decides which index a regression trips, and the two cases are worth keeping apart. Shouting every level fails at the
    /// meta-category, the first thing written, so it never reaches the asset; shouting the asset alone is what actually happened in the field and is the only
    /// one that reaches <c>IX_Asset_Code</c>.
    /// </remarks>
    private enum CodeCasing
    {
        AsWritten,
        EveryLevelShouted,
        AssetOnlyShouted
    }

    [Test, Description("An asset whose code was re-cased on its own updates the stored row instead of failing the save, which is the catalogue edit that broke the Prisoners of War seed.")]
    public async Task ShouldUpdateTheStoredAssetWhenOnlyItsOwnCodeWasRecasedAsync()
    {
        await UpsertAsync(BuildCatalogue());

        await UpsertAsync(BuildCatalogue(CodeCasing.AssetOnlyShouted));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(context.Assets.Count(), Is.EqualTo(1), "The asset was inserted again rather than matched.");
    }

    [Test, Description("A code whose casing alone has changed updates the row already stored at every level, since each level is matched through its own lookup and each is its own chance to miss.")]
    public async Task ShouldUpdateTheExistingRowsWhenOnlyTheCasingOfTheCodesChangedAsync()
    {
        await UpsertAsync(BuildCatalogue());

        await UpsertAsync(BuildCatalogue(CodeCasing.EveryLevelShouted));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.Multiple(() =>
        {
            Assert.That(context.AssetMetaCategories.Count(), Is.EqualTo(1), "The meta-category was inserted again rather than matched.");
            Assert.That(context.AssetCategories.Count(), Is.EqualTo(1), "The category was inserted again rather than matched.");
            Assert.That(context.AssetGroups.Count(), Is.EqualTo(1), "The group was inserted again rather than matched.");
            Assert.That(context.Assets.Count(), Is.EqualTo(1), "The asset was inserted again rather than matched.");
        });
    }

    [Test, Description("A matched row keeps the casing of the code it was first written with, since the upsert reads Code as its match key and never writes it back.")]
    public async Task ShouldLeaveTheStoredCodeCasingAloneWhenMatchingAsync()
    {
        await UpsertAsync(BuildCatalogue());

        await UpsertAsync(BuildCatalogue(CodeCasing.EveryLevelShouted));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.Multiple(() =>
        {
            Assert.That(context.AssetMetaCategories.Single().Code, Is.EqualTo(_META_CODE));
            Assert.That(context.AssetCategories.Single().Code, Is.EqualTo(_CATEGORY_CODE));
            Assert.That(context.AssetGroups.Single().Code, Is.EqualTo(_GROUP_CODE));
            Assert.That(context.Assets.Single().Code, Is.EqualTo(_ASSET_CODE));
        });
    }

    [Test, Description("The display name on a row matched only by a re-cased code is still updated, so a casing change is an ordinary edit rather than a pass that quietly does nothing.")]
    public async Task ShouldStillApplyTheEditWhenTheCodeWasMatchedByCasingAsync()
    {
        await UpsertAsync(BuildCatalogue());

        await UpsertAsync(BuildCatalogue(CodeCasing.AssetOnlyShouted, assetDisplayName: "Seasoned Oak Log"));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(context.Assets.Single().DisplayName, Is.EqualTo("Seasoned Oak Log"));
    }

    /// <summary>
    /// Sends one catalogue through the handler and asserts it was accepted.
    /// </summary>
    /// <remarks>
    /// A fresh context per upsert, the way the seed job is given one per request. It matters here more than it would in-memory: sharing a context would leave
    /// the first pass's entities tracked, and the handler would find them without the round trip whose casing is the whole point of the fixture.
    /// </remarks>
    private async Task UpsertAsync(IReadOnlyCollection<UpsertAssetMetaCategoryDto> metaCategories)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var handler = new UpsertAssetsHandler(new NullLogger<UpsertAssetsHandler>(), context);

        ServiceResultBase result = await handler.Handle(new UpsertAssetsRequest(metaCategories), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, $"Upsert failed: {string.Join("; ", result.Errors)}");
    }

    /// <summary>
    /// One meta-category holding one grouped category with a group of one asset, which is the smallest shape that still has a code at all four levels.
    /// </summary>
    /// <param name="casing">Which levels have their codes shouted, changing nothing but the casing the handler has to match on.</param>
    /// <param name="assetDisplayName">The asset's display name, so a test can prove the matched row was actually edited.</param>
    private static IReadOnlyCollection<UpsertAssetMetaCategoryDto> BuildCatalogue(
        CodeCasing casing = CodeCasing.AsWritten,
        string assetDisplayName = "Oak Log")
    {
        string Parent(string code)
        {
            return casing == CodeCasing.EveryLevelShouted ? code.ToUpperInvariant() : code;
        }

        string AssetCode(string code)
        {
            return casing == CodeCasing.AsWritten ? code : code.ToUpperInvariant();
        }

        return
        [
            new UpsertAssetMetaCategoryDto
            {
                Code = Parent(_META_CODE),
                DisplayName = "Construction Materials",
                IconPath = "/resources/wood/oak-log_40px.png",
                Categories =
                [
                    new UpsertAssetCategoryDto
                    {
                        Code = Parent(_CATEGORY_CODE),
                        DisplayName = "Wood",
                        CategoryType = AssetCategoryType.GroupedAssets,
                        Groups =
                        [
                            new UpsertAssetGroupDto
                            {
                                Code = Parent(_GROUP_CODE),
                                DisplayName = "Logs",
                                Assets =
                                [
                                    new UpsertAssetDto
                                    {
                                        Code = AssetCode(_ASSET_CODE),
                                        DisplayName = assetDisplayName,
                                        IconPath = "/resources/wood/oak-log_40px.png"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        ];
    }
}
