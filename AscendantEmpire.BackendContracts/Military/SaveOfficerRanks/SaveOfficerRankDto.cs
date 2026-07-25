using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.Military.SaveOfficerRanks;

/// <summary>
/// One officer rank level in a bulk save (PUT) of the calling player's rank ladders. The whole set is sent
/// and upserted by (domain, level).
/// </summary>
public record SaveOfficerRankDto
{
    public required DomainType MovementDomain { get; init; }

    /// <summary>1-based seniority level (1..<see cref="Domain.Entities.Military.PlayerOfficerRank.MAX_LEVEL"/>).</summary>
    public required int Level { get; init; }

    /// <summary>The domain-specific title. Required when <see cref="InUse"/>; may be blank for an unused level.</summary>
    public required string Title { get; init; }

    /// <summary>Hard soldier ceiling, or null for the top rank (no ceiling of its own).</summary>
    public int? MaxSoldiers { get; init; }

    public required int MaxAttachedSpecialists { get; init; }

    public required bool InUse { get; init; }
}
