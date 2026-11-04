using System.Reflection;

using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests that the authored storage class catalogue is internally consistent.
/// </summary>
/// <remarks>
/// No database: every check is over what <see cref="StorageClassSeedData.Build"/> returns, so an authoring mistake fails here in milliseconds rather than
/// as a constraint violation part-way through a seed run. Codes are compared case-insensitively throughout, because the unique index on
/// <c>assets.StorageClass.Code</c> is - two classes differing only in case are a clash, not two classes.
/// </remarks>
[TestFixture]
internal class StorageClassSeedDataTests
{
    private static readonly StringComparer _CODE_COMPARER = StringComparer.OrdinalIgnoreCase;

    [Test, Description("No two classes share a code, because the upsert matches on code and would otherwise update whichever it happened to keep.")]
    public void ShouldGiveEveryClassAUniqueCode()
    {
        string[] duplicates = [.. StorageClassSeedData.Build()
            .GroupBy(storageClass => storageClass.Code, _CODE_COMPARER)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];

        Assert.That(duplicates, Is.Empty, $"Duplicate storage class code(s): {string.Join(", ", duplicates)}");
    }

    [Test, Description("No two classes share a display name, since the unique index refuses it and a capacity refusal naming a class would be unexplainable.")]
    public void ShouldGiveEveryClassAUniqueDisplayName()
    {
        string[] duplicates = [.. StorageClassSeedData.Build()
            .GroupBy(storageClass => storageClass.DisplayName, _CODE_COMPARER)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];

        Assert.That(duplicates, Is.Empty, $"Duplicate storage class display name(s): {string.Join(", ", duplicates)}");
    }

    [Test, Description("Every code and display name fits its column, so the seed fails here rather than on a truncation at insert time.")]
    public void ShouldKeepEveryCodeAndNameWithinItsColumnLength()
    {
        IReadOnlyCollection<UpsertStorageClassDto> storageClasses = StorageClassSeedData.Build();

        using (Assert.EnterMultipleScope())
        {
            foreach (UpsertStorageClassDto storageClass in storageClasses)
            {
                Assert.That(storageClass.Code, Has.Length.LessThanOrEqualTo(StorageClass.MAX_CODE_LENGTH));
                Assert.That(storageClass.DisplayName, Has.Length.LessThanOrEqualTo(StorageClass.MAX_DISPLAY_NAME_LENGTH));
            }
        }
    }

    [Test, Description("Every code constant the seed exposes is a class the seed actually builds, so an asset naming one cannot resolve to nothing.")]
    public void ShouldBuildAClassForEveryExposedCodeConstant()
    {
        // AssetSeedData names its classes through these constants, so a constant with no row behind it would pass the compiler and fail the asset seed.
        string[] exposedCodes = [.. typeof(StorageClassSeedData)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)];

        string[] builtCodes = [.. StorageClassSeedData.Build().Select(storageClass => storageClass.Code)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exposedCodes, Is.Not.Empty);
            Assert.That(exposedCodes.Except(builtCodes, _CODE_COMPARER), Is.Empty, "Code constant(s) with no class behind them.");
            Assert.That(builtCodes.Except(exposedCodes, _CODE_COMPARER), Is.Empty, "Class(es) whose code is not exposed as a constant.");
        }
    }
}
