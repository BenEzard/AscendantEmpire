using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.MapGallery;

/// <summary>
/// Tests for <see cref="RiverNetworkRules"/>: the authoring rules that decide whether a map's rivers are legal.
/// </summary>
/// <remarks>
/// <para>
/// No database. These are the rules themselves, over a dictionary of edges and a set of water tiles, which is the part worth pinning: they decide what
/// a map author can and cannot draw, and they are shared by the editor endpoint and the map importer.
/// </para>
/// <para>
/// The map is 5 x 4 with <b>row 0 all sea</b> unless a test says otherwise, so there is a coast along the top, three rows of land below it, and room
/// for a river to run well inland of the tiles the coast touches.
/// </para>
/// <para>
/// Read the coordinates as tile corners, not tiles. <c>South(x, y)</c> is the horizontal edge under tile (x, y), running from corner (x, y+1) to
/// (x+1, y+1); <c>East(x, y)</c> is the vertical edge to its right, from corner (x+1, y) to (x+1, y+1). So <c>East(0, 1)</c> is the vertical edge that
/// runs from the coastline down into the land, and is the natural first stretch of a river.
/// </para>
/// </remarks>
[TestFixture]
internal class RiverNetworkRulesTests
{
    private const int _MAP_WIDTH = 5;
    private const int _MAP_HEIGHT = 4;

    /// <summary>A map whose edges do not join.</summary>
    private static CampaignMapGeometry NoWrapping()
    {
        return new CampaignMapGeometry(CampaignMapOffsetId: 100, _MAP_WIDTH, _MAP_HEIGHT, WrapsHorizontally: false, WrapsVertically: false);
    }

    /// <summary>A map whose east and west edges join, so a river may run across the seam.</summary>
    private static CampaignMapGeometry WrapsHorizontally()
    {
        return new CampaignMapGeometry(CampaignMapOffsetId: 100, _MAP_WIDTH, _MAP_HEIGHT, WrapsHorizontally: true, WrapsVertically: false);
    }

    /// <summary>The whole of row 0, which stands in for the sea along the top of the map.</summary>
    private static HashSet<Coord> SeaAlongTheTop()
    {
        return [.. Enumerable.Range(0, _MAP_WIDTH).Select(x => new Coord(x, 0))];
    }

    private static RiverEdgeKey South(int x, int y) => new(x, y, RiverEdgeSide.South);

    private static RiverEdgeKey East(int x, int y) => new(x, y, RiverEdgeSide.East);

    private static Dictionary<RiverEdgeKey, RiverWidth> Rivers(params (RiverEdgeKey Edge, RiverWidth Width)[] edges)
    {
        return edges.ToDictionary(entry => entry.Edge, entry => entry.Width);
    }

    // ── Reaching the sea ──────────────────────────────────────────────────────────────────────────────────────────────

    [Test, Description("An edge whose corner touches a sea tile is a river mouth, even though both its banks are land.")]
    public void AnEdgeMeetingTheCoastAtACornerIsAMouth()
    {
        // East(0, 1) is the vertical edge between the two land tiles (0, 1) and (1, 1). Its top corner (1, 1) touches the sea tiles (0, 0) and (1, 0),
        // which is how a river running straight into the coast actually meets the water.
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers((East(0, 1), RiverWidth.Medium));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.That(violations, Is.Empty);
    }

    [Test, Description("An edge floating inland with no river running from it to the coast is refused.")]
    public void AnEdgeWithNoPathToTheSeaIsRefused()
    {
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers((East(2, 2), RiverWidth.Medium));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.Multiple(() =>
        {
            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations.Single().Edge, Is.EqualTo(East(2, 2)));
            Assert.That(violations.Single().Reason, Does.Contain("does not reach the sea"));
        });
    }

    [Test, Description("A river grown inland edge by edge from a mouth is accepted along its whole length.")]
    public void ARiverDrawnFromTheCoastInlandIsAccepted()
    {
        // Down from the coast at corner (1, 1), then along and down again: each edge shares a corner with the one before it.
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers(
            (East(0, 1), RiverWidth.Medium),
            (South(1, 1), RiverWidth.Medium),
            (East(1, 2), RiverWidth.Medium));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.That(violations, Is.Empty);
    }

    [Test, Description("Erasing the inland tip of a river leaves the rest reaching the sea, so it is accepted.")]
    public void ErasingTheInlandTipIsAccepted()
    {
        // The three-edge river above with its last edge dropped, which is what an erase of that edge produces.
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers(
            (East(0, 1), RiverWidth.Medium),
            (South(1, 1), RiverWidth.Medium));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.That(violations, Is.Empty);
    }

    [Test, Description("Erasing an edge in the middle of a river is refused, and names every edge it would cut off from the sea.")]
    public void ErasingAMidRiverEdgeIsRefusedAndNamesTheOrphanedEdges()
    {
        // The three-edge river with its middle edge dropped: the last edge is left with no way back to the coast.
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers(
            (East(0, 1), RiverWidth.Medium),
            (East(1, 2), RiverWidth.Medium));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.Multiple(() =>
        {
            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations.Single().Edge, Is.EqualTo(East(1, 2)));
        });
    }

    [Test, Description("A river crossing a horizontally wrapping seam stays one connected river.")]
    public void ARiverCrossesAWrappingSeamAndStaysConnected()
    {
        // South(4, 1) ends at corner (5, 2), which on a wrapping map is corner (0, 2) — the corner South(0, 1) starts from.
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers(
            (East(0, 1), RiverWidth.Medium),
            (South(0, 1), RiverWidth.Medium),
            (South(4, 1), RiverWidth.Medium));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, WrapsHorizontally(), SeaAlongTheTop());

        Assert.That(violations, Is.Empty);
    }

    [Test, Description("Two separate mouths each seed their own river, and both are accepted in one network.")]
    public void TwoMouthsBothCountAsSources()
    {
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers(
            (East(0, 1), RiverWidth.Medium),
            (East(3, 1), RiverWidth.Large));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.That(violations, Is.Empty);
    }

    // ── Edges that are not edges ──────────────────────────────────────────────────────────────────────────────────────

    [Test, Description("An East edge on the last column of a map that does not wrap faces off it, and is refused.")]
    public void AnEdgeFacingOffANonWrappingMapIsRefused()
    {
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers((East(_MAP_WIDTH - 1, 1), RiverWidth.Medium));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.Multiple(() =>
        {
            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations.Single().Reason, Does.Contain("faces off the map"));
        });
    }

    [Test, Description("The last column's East edge is a real edge shared with column 0 on a map that wraps horizontally.")]
    public void TheSeamEdgeIsAcceptedOnAWrappingMap()
    {
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers((East(_MAP_WIDTH - 1, 1), RiverWidth.Medium));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, WrapsHorizontally(), SeaAlongTheTop());

        Assert.That(violations, Is.Empty);
    }

    [Test, Description("An edge with sea on both banks is refused, because a river between two stretches of sea means nothing.")]
    public void AnEdgeBetweenTwoWaterTilesIsRefused()
    {
        // East(0, 0) runs between (0, 0) and (1, 0), both of which are sea.
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers((East(0, 0), RiverWidth.Large));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.Multiple(() =>
        {
            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations.Single().Reason, Does.Contain("water on both sides"));
        });
    }

    [Test, Description("An edge with sea on one bank and land on the other is a river mouth, and is accepted.")]
    public void AnEdgeBetweenLandAndWaterIsAccepted()
    {
        // South(0, 0) is the coastline itself: sea above it, land below it.
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers((South(0, 0), RiverWidth.Large));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.That(violations, Is.Empty);
    }

    // ── Width ─────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Test, Description("A large river narrowing to medium as it runs inland is accepted.")]
    public void ALargeRiverMayNarrowInland()
    {
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers(
            (East(0, 1), RiverWidth.Large),
            (South(1, 1), RiverWidth.Medium),
            (East(1, 2), RiverWidth.Medium));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.That(violations, Is.Empty);
    }

    [Test, Description("A medium river widening to large as it runs inland is refused, naming the inland edge.")]
    public void AMediumRiverMayNotWidenInland()
    {
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers(
            (East(0, 1), RiverWidth.Medium),
            (South(1, 1), RiverWidth.Large));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.Multiple(() =>
        {
            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations.Single().Edge, Is.EqualTo(South(1, 1)));
            Assert.That(violations.Single().Reason, Does.Contain("cannot run inland of"));
        });
    }

    [Test, Description("A medium tributary joining a large trunk further from the sea is accepted, since it only ever narrows inland.")]
    public void AMediumTributaryMayJoinALargeTrunk()
    {
        // The trunk runs down from the coast and on inland; the tributary joins it at corner (1, 2), one hop further out than the trunk's own edges.
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers(
            (East(0, 1), RiverWidth.Large),
            (East(0, 2), RiverWidth.Large),
            (South(1, 1), RiverWidth.Medium));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.That(violations, Is.Empty);
    }

    [Test, Description("Narrowing an edge already at the coast is refused while a large stretch still runs inland of it.")]
    public void NarrowingASeawardEdgeUnderALargeStretchIsRefused()
    {
        // What the author would produce by setting the mouth edge to Medium and leaving the rest of a Large river in place.
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers(
            (East(0, 1), RiverWidth.Medium),
            (South(1, 1), RiverWidth.Large));

        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop());

        Assert.Multiple(() =>
        {
            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations.Single().Edge, Is.EqualTo(South(1, 1)));
        });
    }

    [Test, Description("A mouth edge is not constrained by width, so a river may enter the sea as either medium or large.")]
    public void AMouthMayBeEitherWidth()
    {
        Dictionary<RiverEdgeKey, RiverWidth> mediumMouth = Rivers((East(0, 1), RiverWidth.Medium));
        Dictionary<RiverEdgeKey, RiverWidth> largeMouth = Rivers((East(3, 1), RiverWidth.Large));

        Assert.Multiple(() =>
        {
            Assert.That(RiverNetworkRules.Validate(mediumMouth, NoWrapping(), SeaAlongTheTop()), Is.Empty);
            Assert.That(RiverNetworkRules.Validate(largeMouth, NoWrapping(), SeaAlongTheTop()), Is.Empty);
        });
    }

    // ── Housekeeping ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Test, Description("A map with no rivers on it at all passes, so a save that only erases the last edge is not refused.")]
    public void AnEmptyNetworkPasses()
    {
        IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(Rivers(), NoWrapping(), SeaAlongTheTop());

        Assert.That(violations, Is.Empty);
    }

    [Test, Description("An edge passed with a width of None is a caller error, since an absent edge is what 'no river' means here.")]
    public void AnEdgeWithNoWidthIsACallerError()
    {
        Dictionary<RiverEdgeKey, RiverWidth> rivers = Rivers((East(0, 1), RiverWidth.None));

        Assert.That(() => RiverNetworkRules.Validate(rivers, NoWrapping(), SeaAlongTheTop()), Throws.ArgumentException);
    }

    [Test, Description("The tiles a caller must know the wetness of cover both banks of an edge and every tile at its two corners.")]
    public void TheTilesToCheckCoverBothBanksAndBothCorners()
    {
        IReadOnlyCollection<Coord> needed = RiverNetworkRules.GetTilesToCheckForWater([East(1, 1)], NoWrapping());

        // East(1, 1) has banks (1, 1) and (2, 1), and corners (2, 1) and (2, 2), which between them touch the six tiles of the 2x3 block around it.
        Assert.That(needed, Is.EquivalentTo(new[]
        {
            new Coord(1, 0), new Coord(2, 0),
            new Coord(1, 1), new Coord(2, 1),
            new Coord(1, 2), new Coord(2, 2)
        }));
    }
}
