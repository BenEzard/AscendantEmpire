namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// What one Observer can see: the tiles it has in view right now, its own tile first.
/// </summary>
/// <remarks>
/// The output of <see cref="FieldOfView.ComputeSeenTiles"/>, shared by the live map read and the witness service so the two describe the same view.
/// </remarks>
internal sealed record ObserverFieldOfView(SightObserver Observer, IReadOnlyCollection<SightTerrain> SeenTiles);
