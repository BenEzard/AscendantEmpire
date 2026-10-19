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
            // Was never mapped, so every observer reached the client as RecordType.None and the whole-map pseudo-observer could not be told apart
            // from a Force that is genuinely watching. The client needs that distinction: only real observers light a tile as currently seen.
            ObserverRecordType = (int)observer.RecordType,
            XCoord = observer.Location.X,
            YCoord = observer.Location.Y,
            SeenTiles = [.. observer.SeenTiles.Select(IObservedCampaignTile (tile) => new DTObservedCampaignTile
            {
                XCoord = tile.XCoord,
                YCoord = tile.YCoord,
                BiomeTypeId = tile.BiomeTypeId,
                CampaignMapGeographyTypeId = tile.CampaignMapGeographyTypeId,
                IsExplored = tile.IsExplored,
                // Passed straight through, null and all: an unexplored Tile has no rivers to report, and substituting an empty set would tell the client the
                // ground is known to have none.
                Rivers = tile.Rivers,
                // Passed straight through for the same reason, and the null carries just as much meaning here: a Tile no Observer can see reports no ownership at
                // all, and standing in wilderness for it would draw a border off the back of ground nobody is watching.
                //
                // The record itself, claimant list and all. ObserverSightPresentationTests pins that, because this mapping lists every field by hand and one left out
                // here reaches the client as null with nothing failing anywhere - which is exactly how the ownership borders came to be missing to begin with.
                Ownership = tile.Ownership,
                Structures = [.. tile.Structures?.Select(structure => new DTStructureStub
                {
                    Id = structure.Id,
                    RecordType = structure.RecordType,
                    OwnerPlayerId = structure.OwnerPlayerId,
                    StructureId = structure.StructureId,
                    StructureCode = structure.StructureCode,
                    SettlementName = structure.SettlementName,
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
                    HasCampaignMovementRemaining = force.HasCampaignMovementRemaining,
                    CampaignMovementRemaining = force.CampaignMovementRemaining,
                    CampaignMovementPoints = force.CampaignMovementPoints,
                    BattleMovementRemaining = force.BattleMovementRemaining
                }) ?? []]
            })]
        })];
    }
}
