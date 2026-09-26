using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

/// <summary>
/// One river edge the server refused, with the width it still holds so the client can put its own copy back.
/// </summary>
/// <remarks>
/// The refused edge is not always one the client asked to change. Widening an edge can be refused because of a river further inland that the request
/// never mentioned, and erasing one can be refused because of the stretch it would cut off — so the edge named here is the one that breaks the rule,
/// which is what the author needs to be told about.
/// </remarks>
public interface IRiverEdgeUpdateFail
{
    /// <summary>The X coordinate of the tile that owns the refused edge.</summary>
    int X { get; }

    /// <summary>The Y coordinate of the tile that owns the refused edge.</summary>
    int Y { get; }

    /// <summary>Which of the owning tile's two edges was refused.</summary>
    RiverEdgeSide Edge { get; }

    /// <summary>The width the edge still has, the request having been rejected in full.</summary>
    RiverWidth OriginalWidth { get; }

    /// <summary>Why the edge was refused, in words meant for the author.</summary>
    string ErrorMessage { get; }
}
