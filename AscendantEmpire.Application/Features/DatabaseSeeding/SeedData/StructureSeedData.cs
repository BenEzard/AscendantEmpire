using AscendantEmpire.BackendContracts.Structures;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The <c>structure.Structures</c> catalogue applied by the database seed.
/// </summary>
internal static class StructureSeedData
{
    #region Codes
    public const string VILLAGE_CODE = "village";
    public const string CAMP_CODE = "camp";
    #endregion

    /// <summary>
    /// Builds the Structure catalogue rows to seed.
    /// </summary>
    public static IReadOnlyCollection<UpsertStructureDto> Build()
    {
        return
        [
            Structure(code: CAMP_CODE, name: "Camp", buildPointsRequired: 175_000, sightRange: 1, mapImagePath: "fire_40px.png"),
            Structure(code: VILLAGE_CODE, name: "Village", buildPointsRequired: 2_800_000, sightRange: 2, mapImagePath: "grass-hut_40px.png"),

            // Nothing builds a watch tower yet, so its work is calibrated against nothing - it is half the village's, for a structure half its size.
            Structure(code: "wooden-watch-tower", name: "Wooden Watch Tower", buildPointsRequired: 1_400_000, sightRange: 3, mapImagePath: "wooden-watch-tower_40px.png",
                canBeBuiltOnBattleMap: true,
                dualMapBuildTrigger: true),

            // A boundary structure rather than one occupying the tile, so it takes the Secondary slot and a boundary position. Nothing builds one yet either.
            Structure(code: "palisade", name: "Palisade", buildPointsRequired: 700_000, sightRange: 1, mapImagePath: "pallisade_40px.png",
                requiresSlotType: TileSlotType.Secondary,
                allowedPositions: PositionOnTile.BoundaryMask,
                canBeBuiltOnBattleMap: true,
                dualMapBuildTrigger: true),

            // The claim marker of docs/design/campaign-map/tile-ownership.md, and a marker rather than a building: planted, not raised, so it costs no work and stands the
            // moment it is placed. Nothing plants one yet - claims wait on the per-round pass.
            Structure(code: "wooden-totem-pole", name: "Wooden Totem Pole", buildPointsRequired: 0, sightRange: 1, mapImagePath: "wood-totem-pole_40px.png",
                requiresSlotType: TileSlotType.Secondary),
        ];
    }

    /// <summary>
    /// One catalogue row, with the fields that are the same for every kind defaulted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things are deliberately <i>not</i> derived, though today's five rows would allow it:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <b><paramref name="name"/> from <paramref name="code"/>.</b> Every name here is its code title-cased, but the code is load-bearing - the upsert matches rows on
    ///     it and the client's artwork is keyed on it - so deriving the display name would make rewording it a code change.
    ///   </description></item>
    ///   <item><description>
    ///     <b><c>FullImagePath</c> from <paramref name="mapImagePath"/>.</b> Defaulted to it below, because no full-size artwork has been drawn for any kind yet. That is a
    ///     placeholder state the column exists to grow out of, so expect this to become a parameter rather than a default.
    ///   </description></item>
    /// </list>
    /// </remarks>
    /// <param name="code">The kind's stable catalogue code, which the upsert matches on and the client's artwork is keyed on.</param>
    /// <param name="name">The kind's display name. Never the name of an individual Structure of that kind.</param>
    /// <param name="buildPointsRequired">The work it takes, raw Scale-1,000. 0 for a marker that is placed rather than built. See <c>ConstructionRateCalculator</c> and <c>docs/design/settlements.md</c></param>
    /// <param name="sightRange">How far it sees. At least 1, since a Structure always sees the tile it stands on.</param>
    /// <param name="mapImagePath">The 40px map sprite's file name.</param>
    /// <param name="asserts">
    /// What a finished Structure of this kind asserts over its tile. Defaults to <see cref="StructureAssertion.Owned"/>, which every kind authored so far takes: the
    /// claim tier exists for the markers a scout plants, and nothing plants one yet.
    /// </param>
    private static UpsertStructureDto Structure(
        string code,
        string name,
        long buildPointsRequired,
        int sightRange,
        string mapImagePath,
        TileSlotType requiresSlotType = TileSlotType.Primary,
        PositionOnTile allowedPositions = PositionOnTile.WholeTile,
        bool canBeBuiltOnBattleMap = false,
        bool dualMapBuildTrigger = false,
        StructureAssertion asserts = StructureAssertion.Owned)
    {
        return new UpsertStructureDto
        {
            Code = code,
            Name = name,
            RequiresSlotType = requiresSlotType,
            AllowedPositions = allowedPositions,
            CanBeBuiltOnCampaignMap = true,
            CanBeBuiltOnBattleMap = canBeBuiltOnBattleMap,
            DualMapBuildTrigger = dualMapBuildTrigger,
            BuildPointsRequired = buildPointsRequired,
            SightRange = sightRange,
            SightDirection = DetectionDirection.FullRadius,
            Asserts = asserts,
            MapImagePath = mapImagePath,
            FullImagePath = mapImagePath
        };
    }
}
