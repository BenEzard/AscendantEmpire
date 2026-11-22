using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.Biomes;

/// <summary>
/// Tests for <see cref="MaterialAssetReader"/>: which of a biome's three material values can be named as an Asset.
/// </summary>
/// <remarks>
/// On the in-memory tier. The reader is one LINQ query over the Asset catalogue and nothing else.
/// </remarks>
[TestFixture]
internal class MaterialAssetReaderTests : InMemoryTestBase
{
    private MaterialAssetReader _reader = null!;

    [SetUp]
    public void SetUpReader()
    {
        _reader = new MaterialAssetReader();
    }

    #region Reading the ids

    [Test, Description("Pins that every material resource resolves to its Asset id, so a harvest can name what it banks.")]
    public async Task ShouldReturnAnIdForEveryResourceWhenAllThreeAssetsAreSeededAsync()
    {
        Dictionary<MaterialResource, int> seeded = await SeedAssetsAsync(MaterialResource.Wood, MaterialResource.Stone, MaterialResource.Bone);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyDictionary<MaterialResource, int> idsByResource = await _reader.GetAssetIdsAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(idsByResource, Has.Count.EqualTo(3));
            Assert.That(idsByResource[MaterialResource.Wood], Is.EqualTo(seeded[MaterialResource.Wood]));
            Assert.That(idsByResource[MaterialResource.Stone], Is.EqualTo(seeded[MaterialResource.Stone]));
            Assert.That(idsByResource[MaterialResource.Bone], Is.EqualTo(seeded[MaterialResource.Bone]));
        });
    }

    [Test, Description("Pins that a material whose Asset is missing from the catalogue is left out rather than failing the read, since a seeding fault must not stop a Round.")]
    public async Task ShouldOmitTheResourceWhenItsAssetIsNotInTheCatalogueAsync()
    {
        Dictionary<MaterialResource, int> seeded = await SeedAssetsAsync(MaterialResource.Wood, MaterialResource.Stone);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyDictionary<MaterialResource, int> idsByResource = await _reader.GetAssetIdsAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(idsByResource, Has.Count.EqualTo(2));
            Assert.That(idsByResource.ContainsKey(MaterialResource.Bone), Is.False);
            Assert.That(idsByResource[MaterialResource.Wood], Is.EqualTo(seeded[MaterialResource.Wood]));
        });
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Writes an Asset carrying each named resource's catalogue code, and hands back the id each one was given.
    /// </summary>
    private async Task<Dictionary<MaterialResource, int>> SeedAssetsAsync(params MaterialResource[] resources)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var assetsByResource = new Dictionary<MaterialResource, Asset>();

        foreach (MaterialResource resource in resources)
        {
            var asset = new Asset
            {
                Code = MaterialResourceAssets.CodesByResource[resource],
                DisplayName = resource.ToString(),
                IconPath = "/resources/other/placeholder.svg",
                IsGradedAsset = false,
                SortOrder = 0,
                StorageClassId = 1
            };

            context.Assets.Add(asset);
            assetsByResource.Add(resource, asset);
        }

        await context.SaveChangesAsync();

        return assetsByResource.ToDictionary(entry => entry.Key, entry => entry.Value.Id);
    }

    #endregion
}
