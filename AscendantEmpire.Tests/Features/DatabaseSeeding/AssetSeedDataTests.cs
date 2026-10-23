using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests that the seeded asset catalogue is internally coherent and fits the columns and constraints the database will hold it in.
/// </summary>
/// <remarks>
/// <para>
/// No database — <see cref="AssetSeedData"/> is a static builder and every check here is over the payload it returns, before any handler sees it.
/// </para>
/// <para>
/// These pin what the upsert cannot: that no two rows collide on a code, that every code fits its column, and that each category populates the one of its two
/// child collections its <c>CategoryType</c> says it uses. A payload that fails any of them would be rejected by the handler or the database at seed time, with
/// a far less obvious message.
/// </para>
/// </remarks>
[TestFixture]
internal class AssetSeedDataTests
{
    private readonly IReadOnlyCollection<UpsertAssetMetaCategoryDto> _metaCategories = AssetSeedData.Build();

    [Test, Description("Every meta-category, category and group spells its code out, since everything beneath one derives its own code from that prefix.")]
    public void ShouldSpellOutCodesForEveryLevelAboveAAsset()
    {
        foreach (UpsertAssetMetaCategoryDto metaCategory in _metaCategories)
        {
            Assert.That(metaCategory.Code, Is.Not.Null.And.Not.Empty, $"Meta-category '{metaCategory.DisplayName}' has no code.");

            foreach (UpsertAssetCategoryDto category in metaCategory.Categories)
            {
                Assert.That(category.Code, Is.Not.Null.And.Not.Empty, $"Category '{category.DisplayName}' has no code.");

                foreach (UpsertAssetGroupDto group in category.Groups)
                {
                    Assert.That(group.Code, Is.Not.Null.And.Not.Empty, $"Group '{group.DisplayName}' has no code.");
                }
            }
        }
    }

    [Test, Description("No two rows at the same level share a code, because the upsert matches on code and would otherwise update whichever it happened to keep.")]
    public void ShouldGiveEveryRowAUniqueCodeWithinItsLevel()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_metaCategories.Select(metaCategory => metaCategory.Code), Is.Unique, "Meta-category codes are not unique.");
            Assert.That(AllCategories().Select(category => category.Code), Is.Unique, "Category codes are not unique.");
            Assert.That(AllGroups().Select(group => group.Code), Is.Unique, "Group codes are not unique.");
            Assert.That(AllAssetCodes(), Is.Unique, "Asset codes are not unique.");
        });
    }

    [Test, Description("Every code fits the column it is stored in, so the seed fails here rather than on a truncation at insert time.")]
    public void ShouldKeepEveryCodeWithinItsColumnLength()
    {
        Assert.Multiple(() =>
        {
            foreach (UpsertAssetMetaCategoryDto metaCategory in _metaCategories)
            {
                Assert.That(metaCategory.Code!, Has.Length.LessThanOrEqualTo(AssetMetaCategory.MAX_CODE_LENGTH), $"Meta-category code '{metaCategory.Code}' is too long.");
            }

            foreach (UpsertAssetCategoryDto category in AllCategories())
            {
                Assert.That(category.Code!, Has.Length.LessThanOrEqualTo(AssetCategory.MAX_CODE_LENGTH), $"Category code '{category.Code}' is too long.");
            }

            foreach (UpsertAssetGroupDto group in AllGroups())
            {
                Assert.That(group.Code!, Has.Length.LessThanOrEqualTo(AssetGroup.MAX_CODE_LENGTH), $"Group code '{group.Code}' is too long.");
            }

            foreach (string code in AllAssetCodes())
            {
                Assert.That(code, Has.Length.LessThanOrEqualTo(Asset.MAX_CODE_LENGTH), $"Asset code '{code}' is too long.");
            }
        });
    }

    [Test, Description("Every display name and description fits its column, so no flavour text is silently truncated at insert time.")]
    public void ShouldKeepEveryNameAndDescriptionWithinItsColumnLength()
    {
        Assert.Multiple(() =>
        {
            foreach (UpsertAssetDto asset in AllAssets())
            {
                Assert.That(asset.DisplayName, Has.Length.LessThanOrEqualTo(Asset.MAX_DISPLAY_NAME_LENGTH), $"Asset name '{asset.DisplayName}' is too long.");

                if (asset.Description is not null)
                {
                    Assert.That(
                        asset.Description,
                        Has.Length.LessThanOrEqualTo(Asset.MAX_DESCRIPTION_LENGTH),
                        $"Description of '{asset.DisplayName}' is too long.");
                }
            }
        });
    }

    [Test, Description("A category fills exactly the one child collection its CategoryType uses, so no Asset is silently dropped by the upsert.")]
    public void ShouldPopulateOnlyTheChildCollectionTheCategoryTypeUses()
    {
        Assert.Multiple(() =>
        {
            foreach (UpsertAssetCategoryDto category in AllCategories())
            {
                if (category.CategoryType == AssetCategoryType.GroupedAssets)
                {
                    Assert.That(category.Groups, Is.Not.Empty, $"Grouped category '{category.DisplayName}' has no groups.");
                    Assert.That(category.Assets, Is.Empty, $"Grouped category '{category.DisplayName}' also lists assets directly, which the upsert ignores.");
                }
                else
                {
                    Assert.That(category.Assets, Is.Not.Empty, $"Standalone category '{category.DisplayName}' has no assets.");
                    Assert.That(category.Groups, Is.Empty, $"Standalone category '{category.DisplayName}' also lists groups, which the upsert ignores.");
                }
            }
        });
    }

    [Test, Description("A graded Asset is stocked once per grade under one display name, and each of those rows carries its own code.")]
    public void ShouldStockEachGradedAssetOncePerGrade()
    {
        IEnumerable<IGrouping<string, UpsertAssetDto>> gradedByName = AllAssets()
            .Where(asset => asset.IsGradedAsset)
            .GroupBy(asset => asset.DisplayName);

        Assert.Multiple(() =>
        {
            foreach (IGrouping<string, UpsertAssetDto> grades in gradedByName)
            {
                Assert.That(
                    grades.Select(asset => asset.AssetGrade),
                    Is.EquivalentTo(new[] { AssetGrade.Low, AssetGrade.Medium, AssetGrade.High }),
                    $"Graded asset '{grades.Key}' does not have exactly one row per grade.");

                foreach (UpsertAssetDto asset in grades)
                {
                    Assert.That(asset.Code, Is.Not.Null.And.Not.Empty, $"Graded asset '{grades.Key}' must spell its code out, since every grade shares the display name.");
                }
            }
        });
    }

    [Test, Description("An ungraded Asset carries no grade, so nothing shows a quality shield on something that has no qualities.")]
    public void ShouldLeaveUngradedAssetsWithoutAGrade()
    {
        UpsertAssetDto[] gradedButNotFlagged = [.. AllAssets().Where(r => r.IsGradedAsset == false && r.AssetGrade != AssetGrade.None)];

        Assert.That(gradedButNotFlagged.Select(r => r.DisplayName), Is.Empty, "Ungraded assets carrying a grade.");
    }

    [Test, Description("Every icon path is a root-relative public URL with forward slashes, since it is written to the database and used as an img src verbatim.")]
    public void ShouldPointEveryIconAtARootRelativePublicUrl()
    {
        IEnumerable<string> iconPaths = _metaCategories
            .Select(metaCategory => metaCategory.IconPath)
            .Concat(AllAssets().Select(asset => asset.IconPath))
            .OfType<string>()
            .Where(path => path.Length > 0);

        Assert.Multiple(() =>
        {
            foreach (string path in iconPaths)
            {
                Assert.That(path, Does.StartWith("/resources/"), $"Icon path '{path}' is not a root-relative public URL.");
                Assert.That(path, Does.Not.Contain("\\"), $"Icon path '{path}' uses backslashes.");
            }
        });
    }

    [Test, Description("Every starter asset code game setup demands resolves to a seeded Asset, so a rename in either file fails here rather than at game creation.")]
    public void ShouldSeedAAssetForEveryStarterCode()
    {
        string[] missingCodes = [.. GameSetupService.StarterAssetCodes.Except(AllAssetCodes(), StringComparer.OrdinalIgnoreCase)];

        Assert.That(missingCodes, Is.Empty, $"Starter asset codes with no seeded Asset: {string.Join(", ", missingCodes)}");
    }

    [Test, Description("Every starter meta-category code game setup demands resolves to a seeded AssetMetaCategory, on the same terms as the asset codes.")]
    public void ShouldSeedAMetaCategoryForEveryStarterCode()
    {
        IEnumerable<string> seededCodes = _metaCategories.Select(metaCategory => metaCategory.Code!);

        string[] missingCodes = [.. GameSetupService.StarterAssetMetaCategoryCodes.Except(seededCodes, StringComparer.OrdinalIgnoreCase)];

        Assert.That(missingCodes, Is.Empty, $"Starter meta-category codes with no seeded row: {string.Join(", ", missingCodes)}");
    }

    [Test, Description("A per-other-player category holds groups, since the projection copies its groups to every subject and a standalone one would give each of them a bare grid.")]
    public void ShouldGroupTheAssetsUnderEveryPerOtherPlayerCategory()
    {
        UpsertAssetCategoryDto[] templates = [.. AllCategories().Where(category => category.SubjectScope == AssetSubjectScope.PerOtherPlayer)];

        Assert.That(templates, Is.Not.Empty, "the population template is what the per-other-player projection exists for");

        Assert.Multiple(() =>
        {
            foreach (UpsertAssetCategoryDto template in templates)
            {
                Assert.That(template.CategoryType, Is.EqualTo(AssetCategoryType.GroupedAssets), $"{template.Code} is projected per player");
                Assert.That(template.Groups, Is.Not.Empty, $"{template.Code} would project an empty category onto every opponent");
            }
        });
    }

    [Test, Description("The two Experience assets are held per player, so nothing gives them a bank balance that would compete with the research pipeline's columns.")]
    public void ShouldHoldTheExperienceAssetsAtPlayerScope()
    {
        UpsertAssetCategoryDto experience = AllCategories().Single(category => category.Code == "Society.Experience");

        Assert.That(experience.Assets, Is.Not.Empty, "the experience category is what player-scoped storage exists for");

        Assert.Multiple(() =>
        {
            foreach (UpsertAssetDto asset in experience.Assets)
            {
                Assert.That(
                    asset.StorageScope,
                    Is.EqualTo(AssetStorageScope.Player),
                    $"'{asset.DisplayName}' is already held as PlayerResearchState / PlayerUnusedExperience columns");
            }
        });
    }

    [Test, Description("Every population figure is derived, since Civilians comes from the settlements and the rest from the rosters and the holdings ledger.")]
    public void ShouldDeriveEveryPopulationFigure()
    {
        string[] populationCategoryCodes = ["Society.Population", "Society.ForeignPopulation"];

        UpsertAssetDto[] populationAssets =
        [
            .. AllCategories()
                .Where(category => populationCategoryCodes.Contains(category.Code))
                .SelectMany(category => category.Groups.SelectMany(group => group.Assets).Concat(category.Assets))
        ];

        Assert.That(populationAssets, Is.Not.Empty, "the population categories are what derived storage exists for");

        Assert.Multiple(() =>
        {
            foreach (UpsertAssetDto asset in populationAssets)
            {
                Assert.That(asset.StorageScope, Is.EqualTo(AssetStorageScope.Derived), $"'{asset.DisplayName}' has no balance row to read");
            }
        });
    }

    [Test, Description("Everything outside Society is held in a settlement bank, so a tangible asset cannot be seeded somewhere the drawdown service will not look.")]
    public void ShouldHoldEveryTangibleAssetInASettlementBank()
    {
        IEnumerable<UpsertAssetDto> tangibleAssets = _metaCategories
            .Where(metaCategory => metaCategory.Code != "Society")
            .SelectMany(metaCategory => metaCategory.Categories)
            .SelectMany(category => category.Groups.SelectMany(group => group.Assets).Concat(category.Assets));

        Assert.Multiple(() =>
        {
            foreach (UpsertAssetDto asset in tangibleAssets)
            {
                Assert.That(
                    asset.StorageScope,
                    Is.EqualTo(AssetStorageScope.SettlementBank),
                    $"'{asset.DisplayName}' is a tangible asset and belongs in a bank");
            }
        });
    }

    private IEnumerable<UpsertAssetCategoryDto> AllCategories()
    {
        return _metaCategories.SelectMany(metaCategory => metaCategory.Categories);
    }

    private IEnumerable<UpsertAssetGroupDto> AllGroups()
    {
        return AllCategories().SelectMany(category => category.Groups);
    }

    /// <summary>
    /// Every Asset in the catalogue, down both the grouped and the standalone path.
    /// </summary>
    private IEnumerable<UpsertAssetDto> AllAssets()
    {
        return AllGroups()
            .SelectMany(group => group.Assets)
            .Concat(AllCategories().SelectMany(category => category.Assets));
    }

    /// <summary>
    /// The code each Asset will be stored under, deriving the ones that leave it unset exactly as the upsert would.
    /// </summary>
    private IEnumerable<string> AllAssetCodes()
    {
        IEnumerable<string> groupedCodes = AllGroups()
            .SelectMany(group => group.Assets.Select(asset => asset.Code ?? $"{group.Code}.{ProperCode(asset.DisplayName)}"));

        IEnumerable<string> standaloneCodes = AllCategories()
            .SelectMany(category => category.Assets.Select(asset => asset.Code ?? $"{category.Code}.{ProperCode(asset.DisplayName)}"));

        return groupedCodes.Concat(standaloneCodes);
    }

    /// <summary>
    /// Mirrors the upsert's code derivation, so a derived code is length-checked and collision-checked as the one that will really be stored.
    /// </summary>
    private static string ProperCode(string name)
    {
        return string.Concat(
            System.Text.RegularExpressions.Regex.Split(name.Trim(), "[^a-zA-Z]+")
                .Where(word => word.Length > 0)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }
}
