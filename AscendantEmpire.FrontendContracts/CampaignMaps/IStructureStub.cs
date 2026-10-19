using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface IStructureStub
{
    public int Id { get; }
    public int StructureId { get; }

    /// <summary>
    /// The kind's stable catalogue code, e.g. <c>village</c>. What the client's artwork is keyed on.
    /// </summary>
    /// <remarks>
    /// The id is still sent and is still what the last-known-structure marker matches on. The code decides which sprite is drawn, because catalogue ids are database
    /// identities and differ between databases - keying art on one meant the seed had to hand out the same numbers everywhere.
    /// </remarks>
    public string StructureCode { get; }

    /// <summary>
    /// The name of the Settlement standing here, or <see langword="null"/> when this Structure isn't a Settlement.
    /// </summary>
    /// <remarks>Drawn as a label above the Structure. Reported to anyone who can see the Tile, which is everyone this collection is sent to.</remarks>
    public string? SettlementName { get; }

    public int OwnerPlayerId { get; }
    public PositionOnTile PositionOnTile { get; }
    public double CompletionPercent { get; }
}
