using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// Maps the fog-of-war read's <see cref="ObserverSight"/> results onto the presentation types the API returns.
/// </summary>
/// <remarks>
/// Two endpoints hand back a player's view of the map — the map read itself, and a Force step, which returns the view from where the Force now
/// stands. They must produce identical shapes, because the client merges both into the same tile cache with the same code, so the mapping lives
/// here rather than being written out twice.
/// </remarks>
public static class ObserverSightPresentation
{
    /// <summary>
    /// Converts a collection of observer sight results into the observers the API returns.
    /// </summary>
    public static IReadOnlyCollection<IObserver> ToPresentation(this IEnumerable<ObserverSight> observers)
    {
        ArgumentNullException.ThrowIfNull(observers);

        return [.. observers.Select(IObserver (observer) => new DTObserver
        {
            Id = observer.Id,
            XCoord = observer.Location.X,
            YCoord = observer.Location.Y,
            SeenTiles = [.. observer.SeenTiles.Select(IObservedCampaignTile (tile) => new DTObservedCampaignTile
            {
                XCoord = tile.XCoord,
                YCoord = tile.YCoord,
                BiomeTypeId = tile.BiomeTypeId,
                CampaignMapGeographyTypeId = tile.CampaignMapGeographyTypeId,
                Structures = [.. tile.Structures?.Select(structure => new DTStructureStub
                {
                    Id = structure.Id,
                    RecordType = structure.RecordType,
                    OwnerPlayerId = structure.OwnerPlayerId,
                    StructureId = structure.StructureId,
                    PositionOnTile = structure.PositionOnTile,
                    CompletionPercent = structure.CompletionPercent
                }) ?? []],
                Forces = [.. tile.Forces?.Select(force => new DTForceStub
                {
                    Id = force.Id,
                    RecordType = force.RecordType,
                    OwnerPlayerId = force.OwnerPlayerId,
                    UnitTemplateId = force.UnitTemplateId,
                    HealthyTroops = force.HealthyTroops,
                    InjuredTroops = force.InjuredTroops,
                    InitialStrength = force.InitialStrength,
                    HasMovementRemaining = force.HasMovementRemaining,
                    MovementPointsRemaining = force.MovementPointsRemaining,
                    CampaignMovementPoints = force.CampaignMovementPoints
                }) ?? []]
            })]
        })];
    }
}
