using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Structures;

public class Structure
{
    public const int CODE_MAX_LENGTH = 50;
    public const int NAME_MAX_LENGTH = 100;

    public int Id { get; set; }
    public int RecordTypeId { get; set; }

    [MaxLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    /// <summary>
    /// The type of slot this Structure can be built in. 
    /// </summary>
    public required TileSlotType RequiresSlotType { get; set; }

    /// <summary>
    /// The possible positions this Structure can be built in on a Tile.
    /// </summary>
    public required PositionOnTile AllowedPositions { get; set; }

    public required bool CanBeBuiltOnCampaignMap { get; set; }
    public required bool CanBeBuiltOnBattleMap { get; set; }

    /// <summary>
    /// Denotes that building this Structure on a Campaign Map will also trigger its construction on the Battle Map.
    /// </summary>
    public required bool DualMapBuildTrigger { get; set; }

    /// <summary>
    /// How much construction work this kind of Structure takes, from founding to finished.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Work, not Turns.</b> How long a build takes depends on who is building it: the <c>BuildingStructure</c> end-of-Turn process adds the founding Force's
    /// <see cref="Games.StructureInstance.BuildPointsPerTurn"/> each Turn until this total is met, so a bigger or fresher Force finishes sooner. See
    /// <c>ConstructionRateCalculator</c> for the rate, and <c>docs/design/settlements.md</c> for the calibration.
    /// </para>
    /// <para>
    /// 0 means "no work at all", which finishes at the end of the Turn it was founded in. A Structure placed already <i>finished</i> is a different thing: it is founded
    /// with its <see cref="Games.StructureInstance.BuildPointsProgress"/> already at this total, which <c>IStructureLifecycleWriter.FoundAsync</c> handles, and never
    /// reaches the construction process. The two cannot be confused, because a kind that takes no work has no total to be founded at.
    /// </para>
    /// </remarks>
    public required long BuildPointsRequired { get; set; }

    public required int SightRange { get; set; }
    public required DetectionDirection SightDirection { get; set; }

    /// <summary>
    /// What a completed Structure of this kind asserts over the tile it stands on - a claim, or ownership.
    /// </summary>
    /// <remarks>
    /// Read by the <c>BuildingStructure</c> end-of-Turn process when a Structure finishes, and by nothing else. It sits beside the sight values because it is the same
    /// kind of fact: something the catalogue says about a kind rather than about any individual Structure. See <see cref="StructureAssertion"/> and "Structures" in
    /// <c>docs/design/campaign-map/tile-ownership.md</c>.
    /// </remarks>
    public required StructureAssertion Asserts { get; set; }
    public required string MapImagePath { get; set; }
    public required string FullImagePath { get; set; }
}
