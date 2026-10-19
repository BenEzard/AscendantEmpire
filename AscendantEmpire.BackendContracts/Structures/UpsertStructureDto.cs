using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.BackendContracts.Structures;

/// <summary>
/// One kind of Structure to create or update in the catalogue.
/// </summary>
/// <remarks>
/// Matched on <see cref="Code"/>, so a kind whose name or artwork changes but whose code does not is updated rather than duplicated. The code is also what the client's
/// artwork is keyed on, so it is the one field here that cannot be changed freely.
/// </remarks>
public record UpsertStructureDto
{
    /// <summary>
    /// Unique code for the kind of Structure, e.g. <c>village</c> or <c>wooden-watch-tower</c>.
    /// </summary>
    /// <remarks>
    /// Load-bearing in two places: it matches the row on an upsert, and the frontend's artwork table is keyed on it. Renaming one means changing both, which is why it is
    /// authored deliberately rather than derived from <see cref="Name"/>.
    /// </remarks>
    public required string Code { get; init; }

    /// <summary>
    /// The kind's display name, e.g. "Village". Never the name of an individual Structure of that kind.
    /// </summary>
    public required string Name { get; init; }

    public required TileSlotType RequiresSlotType { get; init; }

    public required PositionOnTile AllowedPositions { get; init; }

    public required bool CanBeBuiltOnCampaignMap { get; init; }

    public required bool CanBeBuiltOnBattleMap { get; init; }

    public required bool DualMapBuildTrigger { get; init; }

    /// <summary>
    /// How much construction work this kind takes, raw Scale-1,000. 0 means no work at all.
    /// </summary>
    /// <remarks>How long that is in Turns depends on the Force that founds it; see <c>ConstructionRateCalculator</c>.</remarks>
    public required long BuildPointsRequired { get; init; }

    public required int SightRange { get; init; }

    public required DetectionDirection SightDirection { get; init; }

    /// <summary>The 40px map sprite's file name, as the campaign map draws it.</summary>
    public required string MapImagePath { get; init; }

    /// <summary>The full-size artwork's file name, for the places that show a Structure at more than map size.</summary>
    public required string FullImagePath { get; init; }
}
