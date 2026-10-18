using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

/// <inheritdoc cref="ITileOwnership"/>
/// <remarks>
/// One record rather than loose properties on <see cref="ObservedCampaignTile"/>, for the same reason <see cref="TileRivers"/> is one: the layers travel together, and a tile
/// whose ownership the Player is not entitled to know can then be described by a single <see langword="null"/>.
/// </remarks>
public record class TileOwnership : ITileOwnership
{
    /// <summary>
    /// Wilderness: a tile nobody owns, occupies, claims or has abandoned.
    /// </summary>
    /// <remarks>
    /// A shared instance, because most of a map reads exactly this and every one of those tiles would otherwise allocate an identical record. Still sent rather than left
    /// <see langword="null"/>: it says the Player can see the ground here and nobody holds it, which is a different answer from the <see langword="null"/> that means they
    /// are not being told. Safe to share because the record is immutable and its one collection is empty.
    /// </remarks>
    public static TileOwnership Unclaimed { get; } = new() { Status = TileOwnershipStatus.Unclaimed };

    /// <inheritdoc />
    public TileOwnershipStatus Status { get; init; }

    /// <inheritdoc />
    public int? OwnedByPlayerId { get; init; }

    /// <inheritdoc />
    public int? OccupiedByPlayerId { get; init; }

    /// <inheritdoc />
    public int? LastOwnedByPlayerId { get; init; }

    /// <inheritdoc />
    /// <remarks>
    /// Computed rather than stored, off the two columns above, so it cannot disagree with them. It mirrors <c>CampaignMap.EffectiveHolderPlayerId</c>, which is the same one
    /// line over the entity.
    /// </remarks>
    public int? EffectiveHolderPlayerId => OccupiedByPlayerId ?? OwnedByPlayerId;

    /// <inheritdoc />
    /// <remarks>Defaults to empty rather than null, so a caller never has to null-check the claims of a tile nobody claims - which is nearly all of them.</remarks>
    public IReadOnlyCollection<int> ClaimedByPlayerIds { get; init; } = [];

    /// <inheritdoc />
    public bool IsRebelling { get; init; }
}
