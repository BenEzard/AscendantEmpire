using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.BackendContracts.Technologies;

/// <summary>
/// One thing a Technology unlocks or retires when it is discovered.
/// </summary>
/// <remarks>
/// <para>
/// The target is named by code rather than id, so the seed can be authored against a fresh database. Which catalogue the code is looked up in is decided by
/// <see cref="TargetType"/>: Assets and their meta-categories, unit components, unit templates, or map resource types.
/// </para>
/// <para>
/// A <see cref="TechnologyGrantTargetType.UnitTemplate"/> target can only be named this way if the template actually has a code - <c>UnitTemplate.Code</c> is
/// nullable, so templates without one cannot be granted by a seeded Technology.
/// </para>
/// </remarks>
public record UpsertTechnologyGrantDto
{
    /// <summary>
    /// What kind of thing is granted, which decides the catalogue <see cref="TargetCode"/> is resolved against.
    /// </summary>
    public required TechnologyGrantTargetType TargetType { get; init; }

    /// <summary>
    /// The code of the thing granted, within the catalogue <see cref="TargetType"/> names.
    /// </summary>
    /// <remarks>Must resolve, so whichever catalogue it points at has to be seeded first - which is what the step's dependencies are for.</remarks>
    public required string TargetCode { get; init; }

    /// <summary>
    /// Whether discovering the Technology makes the target available or retires it.
    /// </summary>
    /// <remarks>Defaults to unlocking, which is overwhelmingly the common case.</remarks>
    public TechnologyGrantEffect GrantEffect { get; init; } = TechnologyGrantEffect.Unlock;
}
