using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A global, reusable tag attached to components — either a structural classification (its family/role)
/// or a bonus, distinguished by <see cref="TagType"/>.
/// </summary>
public class Tag
{
    public const int CODE_MAX_LENGTH = 50;
    public const int DISPLAY_NAME_MAX_LENGTH = 100;
    public const int DESCRIPTION_MAX_LENGTH = 200;

    public int Id { get; set; }

    /// <summary>
    /// Unique, stable key for the tag, e.g. "amphibious", "stealth".
    /// </summary>
    [MaxLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    [MaxLength(DISPLAY_NAME_MAX_LENGTH)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// Optional free-text description.
    /// </summary>
    [MaxLength(DESCRIPTION_MAX_LENGTH)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this tag is a structural classification or a bonus (buff/perk).
    /// </summary>
    public required TagType TagType { get; set; }

    /// <summary>
    /// The movement domain this tag belongs to. Only meaningful for <see cref="TagType.MilitaryOrganisationalGroup"/>
    /// tags, where it ties an organisation hierarchy to a domain (so its tier depth is capped by that domain's
    /// officer rank ladder). Null for every other tag type, and for organisation tags not yet assigned a domain.
    /// </summary>
    public DomainType? MovementDomain { get; set; }

    /// <summary>
    /// How formations under this tag are led. Only meaningful for <see cref="TagType.MilitaryOrganisationalGroup"/> tags; null for every other tag type. 
    /// </summary>
    /// <remarks>Authored here (not on the player's structure) so a player cannot opt a hierarchy out of the officer economy by editing their own data.</remarks>
    public LeadershipModel? LeadershipModel { get; set; }

    /// <summary>
    /// The indivisible group size (quantum) for an <see cref="Enums.UnitDesigns.LeadershipModel.Unled"/> tag — every legal unit size is a whole multiple of this. 
    /// Null unless the tag is unled.
    /// </summary>
    public int? UnledBaseSize { get; set; }

    /// <summary>
    /// The size an unled unit takes when created without an explicit choice (e.g. game-start spawns).
    /// </summary>
    /// <remarks>A multiple of <see cref="UnledBaseSize"/>. Null unless the tag is unled.</remarks>
    public int? UnledDefaultSize { get; set; }

    /// <summary>
    /// The largest a single unled unit may be — the authored guardrail that replaces the officer rank ceiling (there is no officer whose ceiling could apply). 
    /// </summary>
    /// <remarks>Null unless the tag is unled.</remarks>
    public int? UnledMaxSize { get; set; }

    /// <summary>
    /// Whether this tag was created by the system (e.g. seeded/built-in) rather than authored by a user.
    /// Defaults to false for user-created tags.
    /// </summary>
    public bool SystemCreated { get; set; }

    public ICollection<UnitComponentTag> ComponentTags { get; set; } = [];
}
