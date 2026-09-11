using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// A coalition that could see a probed tile, with every one of its Observers that did.
/// </summary>
/// <remarks>
/// All the witnesses are kept, not just the best. Which one a report names — "your sentry at Redhill reports" — is the fold's decision, and under shared
/// vision a report carries the fidelity of the Observer that actually saw, which may be an ally's.
/// </remarks>
internal sealed record WitnessingCoalition
{
    public required int CoalitionId { get; init; }

    public required IReadOnlyCollection<int> MemberPlayerIds { get; init; }

    /// <summary>
    /// The coalition's Observers that saw the tile, nearest first. Never empty.
    /// </summary>
    public required IReadOnlyCollection<Witness> Witnesses { get; init; }

    /// <summary>
    /// The most any of the coalition's witnesses made out.
    /// </summary>
    public SightingFidelity BestFidelity => Witnesses.Max(witness => witness.Fidelity);
}

/// <summary>
/// One Observer that saw a probed tile, and how well.
/// </summary>
internal sealed record Witness
{
    public required int ObserverId { get; init; }

    public required RecordType RecordType { get; init; }

    public required int OwnerPlayerId { get; init; }

    public required Coord Location { get; init; }

    /// <summary>
    /// Tiles between the Observer and the probed tile, Chebyshev and wrap-aware.
    /// </summary>
    public required int Distance { get; init; }

    public required SightingFidelity Fidelity { get; init; }
}
