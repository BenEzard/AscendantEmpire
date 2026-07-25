using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.Military.GetOfficerRanks;

/// <summary>
/// A single officer rank level for a player, within a movement domain, as shown on the Military Advisor's
/// Officer Class page.
/// </summary>
public record OfficerRankView
{
    /// <summary>The movement domain this rank commands within.</summary>
    public required DomainType MovementDomain { get; init; }

    /// <summary>1-based seniority level (1 = most junior).</summary>
    public required int Level { get; init; }

    /// <summary>The domain-specific display title (e.g. "General" / "Admiral").</summary>
    public required string Title { get; init; }

    /// <summary>Hard soldier ceiling for a tier commanded at this rank, or null for the top rank.</summary>
    public int? MaxSoldiers { get; init; }

    /// <summary>The most specialists a formation led at this rank may have attached.</summary>
    public required int MaxAttachedSpecialists { get; init; }

    /// <summary>Whether this level is currently in use for the player/domain.</summary>
    public required bool InUse { get; init; }
}
