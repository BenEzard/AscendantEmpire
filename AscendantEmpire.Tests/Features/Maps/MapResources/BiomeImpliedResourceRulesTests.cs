using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Pins the rules that decide which tiles hold the MapResource their Biome implies, and which placements go.
/// </summary>
/// <remarks>
/// No database: <see cref="BiomeImpliedResourceRules"/> is pure by design, so that generating a map and painting a tile in the map editor - which write in
/// completely different ways - cannot drift apart on what they decide. Every rule is pinned here rather than through either caller.
/// </remarks>
[TestFixture]
internal sealed class BiomeImpliedResourceRulesTests
{
    /// <summary>
    /// The MapResourceType being planned for. Any id does, since the rules never look it up.
    /// </summary>
    private const int _TIMBER_TYPE_ID = 7;

    /// <summary>
    /// A second type, standing in for whatever else an author has placed on a tile.
    /// </summary>
    private const int _DEER_TYPE_ID = 4;

    private const int _TILE_ID = 48812;

    [Test, Description("A Biome-wide habitat covers every Geography in that Biome, so a Forest tile of any Geography is given the MapResource.")]
    public void ShouldPlanAPlacementWhenTheBiomeHasABiomeWideHabitat()
    {
        BiomeImpliedResourcePlan plan = BiomeImpliedResourceRules.Plan(
            _TIMBER_TYPE_ID,
            [new HabitatScope(BiomeType.Forest, null)],
            [BuildTile(BiomeType.Forest, CampaignMapGeographyType.Hilly)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.ToAdd, Has.Count.EqualTo(1), "A Forest tile with a Biome-wide Forest habitat should gain the MapResource.");
            Assert.That(plan.ToAdd.Single().Slot, Is.EqualTo(1), "An empty tile's first free slot is slot 1.");
            Assert.That(plan.Refusals, Is.Empty, "Nothing stood in the way, so nothing should be refused.");
        }
    }

    [Test, Description("No habitat for a tile's Biome means the MapResource could never exist there, so an unwooded Biome is left bare.")]
    public void ShouldPlanNothingWhenTheBiomeHasNoHabitat()
    {
        BiomeImpliedResourcePlan plan = BiomeImpliedResourceRules.Plan(
            _TIMBER_TYPE_ID,
            [new HabitatScope(BiomeType.Forest, null)],
            [BuildTile(BiomeType.Grassland, CampaignMapGeographyType.Flat)]);

        Assert.That(plan.IsEmpty, Is.True, "A Grassland tile has no Forest habitat, so there is nothing to add, delete or refuse.");
    }

    [Test, Description("A habitat naming a Geography covers only that Geography, which is what lets an author say wood grows on hilly forest alone.")]
    public void ShouldHonourAGeographySpecificHabitatWhenTheBiomeHasNoBiomeWideRow()
    {
        HabitatScope[] hillyForestOnly = [new HabitatScope(BiomeType.Forest, CampaignMapGeographyType.Hilly)];

        BiomeImpliedResourcePlan hillyPlan = BiomeImpliedResourceRules.Plan(
            _TIMBER_TYPE_ID, hillyForestOnly, [BuildTile(BiomeType.Forest, CampaignMapGeographyType.Hilly)]);

        BiomeImpliedResourcePlan flatPlan = BiomeImpliedResourceRules.Plan(
            _TIMBER_TYPE_ID, hillyForestOnly, [BuildTile(BiomeType.Forest, CampaignMapGeographyType.Flat)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hillyPlan.ToAdd, Has.Count.EqualTo(1), "Hilly Forest is exactly what the habitat names.");
            Assert.That(flatPlan.IsEmpty, Is.True, "Flat Forest is not covered by a habitat naming Hilly, so it should gain nothing.");
        }
    }

    [Test, Description("The slot is chosen from what the tile already holds, so a tile an author has placed something on still gains the MapResource.")]
    public void ShouldTakeTheFirstFreeSlotWhenTheTileAlreadyHoldsAnotherResource()
    {
        BiomeImpliedResourcePlan plan = BiomeImpliedResourceRules.Plan(
            _TIMBER_TYPE_ID,
            [new HabitatScope(BiomeType.Forest, null)],
            [BuildTile(BiomeType.Forest, CampaignMapGeographyType.Flat, BuildPlacement(id: 1, _DEER_TYPE_ID, slot: 1))]);

        Assert.That(plan.ToAdd.Single().Slot, Is.EqualTo(2), "Slot 1 is taken by the other MapResource, so the second slot is the first free one.");
    }

    [Test, Description("A tile that already holds the type contributes nothing, which is what keeps a second run from breaching UX_CampaignMapGalleryResource_Tile_Type.")]
    public void ShouldPlanNothingWhenTheTileAlreadyHoldsTheType()
    {
        BiomeImpliedResourcePlan plan = BiomeImpliedResourceRules.Plan(
            _TIMBER_TYPE_ID,
            [new HabitatScope(BiomeType.Forest, null)],
            [BuildTile(BiomeType.Forest, CampaignMapGeographyType.Flat, BuildPlacement(id: 1, _TIMBER_TYPE_ID, slot: 1))]);

        Assert.That(plan.IsEmpty, Is.True, "The tile is already as it should be, so applying the plan again must change nothing.");
    }

    [Test, Description("A tile holds at most two MapResources, so one an author has filled is refused rather than failed - the tile is full, not wrong.")]
    public void ShouldRefuseWithNoFreeSlotWhenBothSlotsAreTaken()
    {
        BiomeImpliedResourcePlan plan = BiomeImpliedResourceRules.Plan(
            _TIMBER_TYPE_ID,
            [new HabitatScope(BiomeType.Forest, null)],
            [BuildTile(
                BiomeType.Forest,
                CampaignMapGeographyType.Flat,
                BuildPlacement(id: 1, _DEER_TYPE_ID, slot: 1),
                BuildPlacement(id: 2, mapResourceTypeId: 9, slot: 2))]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.ToAdd, Is.Empty, "There is no slot left to place it in.");
            Assert.That(plan.Refusals.Single().Reason, Is.EqualTo(BiomeImpliedResourceRefusal.NoFreeSlot), "A full tile is refused for being full.");
        }
    }

    [Test, Description("A placement nothing was authored on is removed once its tile's Biome no longer holds the MapResource, so no tile keeps wood out of habitat.")]
    public void ShouldPlanADeletionWhenTheNewBiomeHasNoHabitatAndNothingWasAuthored()
    {
        BiomeImpliedResourcePlan plan = BiomeImpliedResourceRules.Plan(
            _TIMBER_TYPE_ID,
            [new HabitatScope(BiomeType.Forest, null)],
            [BuildTile(BiomeType.Desert, CampaignMapGeographyType.Flat, BuildPlacement(id: 55, _TIMBER_TYPE_ID, slot: 1))]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.PlacementIdsToDelete, Is.EqualTo(new[] { 55 }), "The placement on a tile outside the habitat should go.");
            Assert.That(plan.Refusals, Is.Empty, "Nothing was authored on it, so there is nothing to hold it back.");
        }
    }

    [Test, Description("An authored placement is kept and its tile refused, so a brush stroke cannot silently destroy a pinned scenario or a chosen quantity.")]
    public void ShouldRefuseTheDeletionWhenThePlacementWasAuthored()
    {
        // Which column carried the author's intent - a pinned scenario or a set quantity - is decided before the rules see it, and is pinned where that
        // happens in BiomeImpliedMapResourceServiceTests.
        BiomeImpliedResourcePlan plan = BiomeImpliedResourceRules.Plan(
            _TIMBER_TYPE_ID,
            [new HabitatScope(BiomeType.Forest, null)],
            [BuildTile(
                BiomeType.Desert,
                CampaignMapGeographyType.Flat,
                BuildPlacement(id: 55, _TIMBER_TYPE_ID, slot: 1, carriesAuthorIntent: true))]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.PlacementIdsToDelete, Is.Empty, "An authored placement is kept.");
            Assert.That(
                plan.Refusals.Single().Reason,
                Is.EqualTo(BiomeImpliedResourceRefusal.AuthorIntentBlocksRemoval),
                "The refusal is what tells the caller to leave the tile's Biome alone as well.");
        }
    }

    [Test, Description("Tiles that are already right contribute nothing whatever their Biome, so applying a plan twice is harmless.")]
    public void ShouldPlanNothingWhenEveryTileIsAlreadyCorrect()
    {
        BiomeImpliedResourcePlan plan = BiomeImpliedResourceRules.Plan(
            _TIMBER_TYPE_ID,
            [new HabitatScope(BiomeType.Forest, null)],
            [
                BuildTile(BiomeType.Forest, CampaignMapGeographyType.Flat, BuildPlacement(id: 1, _TIMBER_TYPE_ID, slot: 1)),
                BuildTile(BiomeType.Grassland, CampaignMapGeographyType.Flat)
            ]);

        Assert.That(plan.IsEmpty, Is.True, "A wooded tile that already holds wood and a bare tile that should stay bare both need nothing.");
    }

    [Test, Description("A Biome-wide habitat and one naming a Geography both count, so adding the specific row never narrows what the Biome-wide row already covered.")]
    public void ShouldFindAHabitatWhenEitherTheBiomeWideOrTheGeographyRowCovers()
    {
        HabitatScope[] habitats = [new HabitatScope(BiomeType.Forest, null), new HabitatScope(BiomeType.Jungle, CampaignMapGeographyType.Flat)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                BiomeImpliedResourceRules.HasHabitat(habitats, BiomeType.Forest, CampaignMapGeographyType.Mountainous),
                Is.True,
                "The Biome-wide Forest row covers every Geography.");

            Assert.That(
                BiomeImpliedResourceRules.HasHabitat(habitats, BiomeType.Jungle, CampaignMapGeographyType.Flat),
                Is.True,
                "The Jungle row names Flat exactly.");

            Assert.That(
                BiomeImpliedResourceRules.HasHabitat(habitats, BiomeType.Jungle, CampaignMapGeographyType.Hilly),
                Is.False,
                "Hilly Jungle is covered by neither row.");
        }
    }

    // ---------------------------------------------------------------------

    private static TileResourceState BuildTile(BiomeType biome, CampaignMapGeographyType geography, params TilePlacementState[] placements)
    {
        return new TileResourceState(_TILE_ID, XCoord: 4, YCoord: 5, biome, geography, placements);
    }

    private static TilePlacementState BuildPlacement(int id, int mapResourceTypeId, byte slot, bool carriesAuthorIntent = false)
    {
        return new TilePlacementState(id, mapResourceTypeId, slot, carriesAuthorIntent);
    }
}
