namespace AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

/// <summary>
/// The edges that made a river batch illegal, carried as the error detail of the refusal.
/// </summary>
/// <remarks>
/// Structured rather than only worded, because the editor can put a marker on the offending edge — and the offending edge is frequently not the one
/// the author clicked. The error messages beside it stay the human-readable account of what went wrong.
/// </remarks>
public interface IRiverEdgeUpdateFailCollection
{
    /// <summary>Each refused edge, with the width it still holds and why it was refused.</summary>
    IReadOnlyCollection<IRiverEdgeUpdateFail> RefusedEdges { get; }
}
