using AscendantEmpire.Domain.Entities.Military;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Military;

/// <summary>
/// Code-defined default military organisation configuration: the officer rank ladders seeded for every
/// new player, and the fallback organisation structure returned for a tag that has no saved structure yet.
/// </summary>
/// <remarks>
/// <para>
/// These mirror the defaults in <c>docs/design/units/military-organisation.md</c>. They live in code (rather
/// than the database) by design — see the "code-defined constants" decision — so they version with the
/// application and can seed a game without any catalogue rows being present.
/// </para>
/// </remarks>
public static class MilitaryOrganisationDefaults
{
    /// <summary>A single default rank level within a domain's ladder.</summary>
    /// <param name="Level">1-based seniority level.</param>
    /// <param name="Title">The default title for this level in the given domain.</param>
    /// <param name="MaxSoldiers">Hard soldier ceiling, or null for the top rank (no ceiling of its own).</param>
    /// <param name="MaxAttachedSpecialists">The specialist attachment cap at this rank.</param>
    public record RankDefault(int Level, string Title, int? MaxSoldiers, int MaxAttachedSpecialists);

    /// <summary>A single default tier within the fallback organisation structure.</summary>
    /// <param name="TierIndex">1-based tier index (smallest first).</param>
    /// <param name="Name">The default (cosmetic) tier label.</param>
    /// <param name="Multiplier">Tier 1: base soldier count. Tiers above: fan-out of the tier below.</param>
    public record TierDefault(int TierIndex, string Name, int Multiplier);

    /// <summary>
    /// The movement domains a new player's rank ladders are seeded for. NearEarthOrbit is intentionally
    /// excluded for now (no title set defined yet).
    /// </summary>
    public static readonly IReadOnlyList<DomainType> SeededDomains =
    [
        DomainType.Land,
        DomainType.Water,
        DomainType.BelowWater,
        DomainType.Air,
    ];

    // Shared, domain-independent numeric defaults, indexed by level - 1. Ceilings: 44/200/1000/3200/10000
    // then null (the top rank is governed by its max number of Units, not a soldier ceiling).
    private static readonly int?[] _maxSoldiersByLevel = [44, 200, 1_000, 3_200, 10_000, null];
    private static readonly int[] _maxSpecialistsByLevel = [1, 3, 5, 10, 12, 15];

    // Per-domain rank titles, level 1..6 (low to high). Water is used for BelowWater as a placeholder until
    // a distinct submariner title set is chosen.
    private static readonly string[] _landTitles = ["Lieutenant", "Captain", "Major", "Colonel", "Major General", "General"];
    private static readonly string[] _seaTitles = ["Ensign", "Lieutenant", "Commander", "Captain", "Commodore", "Admiral"];
    private static readonly string[] _airTitles = ["Pilot Officer", "Flight Lieutenant", "Squadron Leader", "Wing Commander", "Air Commodore", "Air Marshal"];

    /// <summary>
    /// The default rank ladder for a domain: <see cref="PlayerOfficerRank.MAX_LEVEL"/> levels, every level
    /// in use, seeded with the domain's default titles and the shared ceiling/specialist defaults.
    /// </summary>
    public static IReadOnlyList<RankDefault> RanksForDomain(DomainType domain)
    {
        string[] titles = TitlesForDomain(domain);

        List<RankDefault> ranks = [];
        for (int level = 1; level <= PlayerOfficerRank.MAX_LEVEL; level++)
        {
            int index = level - 1;
            ranks.Add(new RankDefault(
                Level: level,
                Title: titles[index],
                MaxSoldiers: _maxSoldiersByLevel[index],
                MaxAttachedSpecialists: _maxSpecialistsByLevel[index]));
        }

        return ranks;
    }

    /// <summary>
    /// The fallback organisation structure (the doc's "Infantry" shape: 10 / 50 / 100 / 500 / 2000) returned
    /// for a tag the player has not yet customised. Land-flavoured tier labels.
    /// </summary>
    public static readonly IReadOnlyList<TierDefault> DefaultStructureTiers =
    [
        new TierDefault(1, "Company", 10),
        new TierDefault(2, "Platoon", 5),
        new TierDefault(3, "Battalion", 2),
        new TierDefault(4, "Regiment", 5),
        new TierDefault(5, "Division", 4),
    ];

    /// <summary>
    /// The maximum number of tiers a hierarchy may have, given how many officer ranks are in use in its
    /// domain. One fewer than the rank count (the top rank is reserved for the Command-of-units commander),
    /// floored at 1 so a domain can always field a bottom tier, and never above <see cref="PlayerOrganisationStructureTier.MAX_TIERS"/>.
    /// </summary>
    public static int MaxTiersForRankCount(int inUseRankCount)
    {
        return Math.Clamp(inUseRankCount - 1, 1, PlayerOrganisationStructureTier.MAX_TIERS);
    }

    private static string[] TitlesForDomain(DomainType domain) => domain switch
    {
        DomainType.Land => _landTitles,
        DomainType.Air => _airTitles,
        // Water and BelowWater both use the naval title set for now (BelowWater is a placeholder).
        DomainType.Water => _seaTitles,
        DomainType.BelowWater => _seaTitles,
        _ => _landTitles,
    };
}
