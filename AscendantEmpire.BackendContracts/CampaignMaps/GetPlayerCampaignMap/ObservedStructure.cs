using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public record ObservedStructure
{
    public int Id { get; init; }
    public RecordType RecordType { get; init; }
    public int OwnerPlayerId { get; init; }
    public int StructureId { get; init; }

    /// <summary>
    /// The kind's stable catalogue code, e.g. <c>village</c>.
    /// </summary>
    /// <remarks>
    /// What the client's artwork is keyed on. The id is sent too, and is still what the last-known-structure marker matches on, but the code is what decides which sprite is
    /// drawn - catalogue ids are database identities and differ between databases, so keying art on one meant the seed had to hand out the same numbers everywhere.
    /// </remarks>
    public string StructureCode { get; init; } = null!;

    /// <summary>
    /// The name of the Settlement standing here, or <see langword="null"/> if not a Settlement.
    /// </summary>
    /// <remarks>
    /// Drawn as a label above the Structure on the campaign map. Not masked for anybody: a settlement cannot be concealed, and this read reports a Structure only on a tile
    /// the Player can see - being in the response is the permission.
    /// </remarks>
    public string? SettlementName { get; init; }

    public PositionOnTile PositionOnTile { get; init; }
    public double CompletionPercent { get; init; }
}
