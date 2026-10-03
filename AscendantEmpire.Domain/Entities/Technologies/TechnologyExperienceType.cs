namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// Records that one kind of experience feeds one Technology's experience bucket.
/// </summary>
/// <remarks>
/// <para>
/// A pure mapping with <b>no quantity on it</b>. How many points an action is worth is decided by the action at the moment it happens - proportional to how
/// much was extracted, and varying per Player per action - so it cannot be authored against a Technology. Composite primary key on
/// (TechnologyId, ExperienceTypeId).
/// </para>
/// <para>
/// Both sides are real foreign keys, unlike <see cref="TechnologyGrant.TargetId"/>, because an experience type is a single table.
/// </para>
/// </remarks>
public class TechnologyExperienceType
{
    public int TechnologyId { get; set; }

    public int ExperienceTypeId { get; set; }

    public Technology Technology { get; set; } = null!;

    public ExperienceType ExperienceType { get; set; } = null!;
}
