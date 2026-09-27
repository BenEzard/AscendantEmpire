using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.BackendContracts.Resources;
using AscendantEmpire.Domain.Entities.Resources;
using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests that the seeded resource catalogue is internally coherent and fits the columns and constraints the database will hold it in.
/// </summary>
/// <remarks>
/// <para>
/// No database — <see cref="ResourceSeedData"/> is a static builder and every check here is over the payload it returns, before any handler sees it.
/// </para>
/// <para>
/// These pin what the upsert cannot: that no two rows collide on a code, that every code fits its column, and that each category populates the one of its two
/// child collections its <c>CategoryType</c> says it uses. A payload that fails any of them would be rejected by the handler or the database at seed time, with
/// a far less obvious message.
/// </para>
/// </remarks>
[TestFixture]
internal class ResourceSeedDataTests
{
    private readonly IReadOnlyCollection<UpsertResourceMetaCategoryDto> _metaCategories = ResourceSeedData.Build();

    [Test, Description("Every meta-category, category and group spells its code out, since everything beneath one derives its own code from that prefix.")]
    public void ShouldSpellOutCodesForEveryLevelAboveAResource()
    {
        foreach (UpsertResourceMetaCategoryDto metaCategory in _metaCategories)
        {
            Assert.That(metaCategory.Code, Is.Not.Null.And.Not.Empty, $"Meta-category '{metaCategory.DisplayName}' has no code.");

            foreach (UpsertResourceCategoryDto category in metaCategory.Categories)
            {
                Assert.That(category.Code, Is.Not.Null.And.Not.Empty, $"Category '{category.DisplayName}' has no code.");

                foreach (UpsertResourceGroupDto group in category.Groups)
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
            Assert.That(AllResourceCodes(), Is.Unique, "Resource codes are not unique.");
        });
    }

    [Test, Description("Every code fits the column it is stored in, so the seed fails here rather than on a truncation at insert time.")]
    public void ShouldKeepEveryCodeWithinItsColumnLength()
    {
        Assert.Multiple(() =>
        {
            foreach (UpsertResourceMetaCategoryDto metaCategory in _metaCategories)
            {
                Assert.That(metaCategory.Code!, Has.Length.LessThanOrEqualTo(ResourceMetaCategory.MAX_CODE_LENGTH), $"Meta-category code '{metaCategory.Code}' is too long.");
            }

            foreach (UpsertResourceCategoryDto category in AllCategories())
            {
                Assert.That(category.Code!, Has.Length.LessThanOrEqualTo(ResourceCategory.MAX_CODE_LENGTH), $"Category code '{category.Code}' is too long.");
            }

            foreach (UpsertResourceGroupDto group in AllGroups())
            {
                Assert.That(group.Code!, Has.Length.LessThanOrEqualTo(ResourceGroup.MAX_CODE_LENGTH), $"Group code '{group.Code}' is too long.");
            }

            foreach (string code in AllResourceCodes())
            {
                Assert.That(code, Has.Length.LessThanOrEqualTo(Resource.MAX_CODE_LENGTH), $"Resource code '{code}' is too long.");
            }
        });
    }

    [Test, Description("Every display name and description fits its column, so no flavour text is silently truncated at insert time.")]
    public void ShouldKeepEveryNameAndDescriptionWithinItsColumnLength()
    {
        Assert.Multiple(() =>
        {
            foreach (UpsertResourceDto resource in AllResources())
            {
                Assert.That(resource.DisplayName, Has.Length.LessThanOrEqualTo(Resource.MAX_DISPLAY_NAME_LENGTH), $"Resource name '{resource.DisplayName}' is too long.");

                if (resource.Description is not null)
                {
                    Assert.That(
                        resource.Description,
                        Has.Length.LessThanOrEqualTo(Resource.MAX_DESCRIPTION_LENGTH),
                        $"Description of '{resource.DisplayName}' is too long.");
                }
            }
        });
    }

    [Test, Description("A category fills exactly the one child collection its CategoryType uses, so no Resource is silently dropped by the upsert.")]
    public void ShouldPopulateOnlyTheChildCollectionTheCategoryTypeUses()
    {
        Assert.Multiple(() =>
        {
            foreach (UpsertResourceCategoryDto category in AllCategories())
            {
                if (category.CategoryType == ResourceCategoryType.GroupedResources)
                {
                    Assert.That(category.Groups, Is.Not.Empty, $"Grouped category '{category.DisplayName}' has no groups.");
                    Assert.That(category.Resources, Is.Empty, $"Grouped category '{category.DisplayName}' also lists resources directly, which the upsert ignores.");
                }
                else
                {
                    Assert.That(category.Resources, Is.Not.Empty, $"Standalone category '{category.DisplayName}' has no resources.");
                    Assert.That(category.Groups, Is.Empty, $"Standalone category '{category.DisplayName}' also lists groups, which the upsert ignores.");
                }
            }
        });
    }

    [Test, Description("A graded Resource is stocked once per grade under one display name, and each of those rows carries its own code.")]
    public void ShouldStockEachGradedResourceOncePerGrade()
    {
        IEnumerable<IGrouping<string, UpsertResourceDto>> gradedByName = AllResources()
            .Where(resource => resource.IsGradedResource)
            .GroupBy(resource => resource.DisplayName);

        Assert.Multiple(() =>
        {
            foreach (IGrouping<string, UpsertResourceDto> grades in gradedByName)
            {
                Assert.That(
                    grades.Select(resource => resource.ResourceGrade),
                    Is.EquivalentTo(new[] { ResourceGrade.Low, ResourceGrade.Medium, ResourceGrade.High }),
                    $"Graded resource '{grades.Key}' does not have exactly one row per grade.");

                foreach (UpsertResourceDto resource in grades)
                {
                    Assert.That(resource.Code, Is.Not.Null.And.Not.Empty, $"Graded resource '{grades.Key}' must spell its code out, since every grade shares the display name.");
                }
            }
        });
    }

    [Test, Description("An ungraded Resource carries no grade, so nothing shows a quality shield on something that has no qualities.")]
    public void ShouldLeaveUngradedResourcesWithoutAGrade()
    {
        UpsertResourceDto[] gradedButNotFlagged = [.. AllResources().Where(r => r.IsGradedResource == false && r.ResourceGrade != ResourceGrade.None)];

        Assert.That(gradedButNotFlagged.Select(r => r.DisplayName), Is.Empty, "Ungraded resources carrying a grade.");
    }

    [Test, Description("Every icon path is a root-relative public URL with forward slashes, since it is written to the database and used as an img src verbatim.")]
    public void ShouldPointEveryIconAtARootRelativePublicUrl()
    {
        IEnumerable<string> iconPaths = _metaCategories
            .Select(metaCategory => metaCategory.IconPath)
            .Concat(AllResources().Select(resource => resource.IconPath))
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

    [Test, Description("Every starter resource code game setup demands resolves to a seeded Resource, so a rename in either file fails here rather than at game creation.")]
    public void ShouldSeedAResourceForEveryStarterCode()
    {
        string[] missingCodes = [.. GameSetupService.StarterResourceCodes.Except(AllResourceCodes(), StringComparer.OrdinalIgnoreCase)];

        Assert.That(missingCodes, Is.Empty, $"Starter resource codes with no seeded Resource: {string.Join(", ", missingCodes)}");
    }

    [Test, Description("Every starter meta-category code game setup demands resolves to a seeded ResourceMetaCategory, on the same terms as the resource codes.")]
    public void ShouldSeedAMetaCategoryForEveryStarterCode()
    {
        IEnumerable<string> seededCodes = _metaCategories.Select(metaCategory => metaCategory.Code!);

        string[] missingCodes = [.. GameSetupService.StarterResourceMetaCategoryCodes.Except(seededCodes, StringComparer.OrdinalIgnoreCase)];

        Assert.That(missingCodes, Is.Empty, $"Starter meta-category codes with no seeded row: {string.Join(", ", missingCodes)}");
    }

    private IEnumerable<UpsertResourceCategoryDto> AllCategories()
    {
        return _metaCategories.SelectMany(metaCategory => metaCategory.Categories);
    }

    private IEnumerable<UpsertResourceGroupDto> AllGroups()
    {
        return AllCategories().SelectMany(category => category.Groups);
    }

    /// <summary>
    /// Every Resource in the catalogue, down both the grouped and the standalone path.
    /// </summary>
    private IEnumerable<UpsertResourceDto> AllResources()
    {
        return AllGroups()
            .SelectMany(group => group.Resources)
            .Concat(AllCategories().SelectMany(category => category.Resources));
    }

    /// <summary>
    /// The code each Resource will be stored under, deriving the ones that leave it unset exactly as the upsert would.
    /// </summary>
    private IEnumerable<string> AllResourceCodes()
    {
        IEnumerable<string> groupedCodes = AllGroups()
            .SelectMany(group => group.Resources.Select(resource => resource.Code ?? $"{group.Code}.{ProperCode(resource.DisplayName)}"));

        IEnumerable<string> standaloneCodes = AllCategories()
            .SelectMany(category => category.Resources.Select(resource => resource.Code ?? $"{category.Code}.{ProperCode(resource.DisplayName)}"));

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
