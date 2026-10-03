namespace AscendantEmpire.Domain.Enums.Technologies;

/// <summary>
/// Whether discovering a Technology makes its target available or retires it.
/// </summary>
/// <remarks>
/// Without the <see cref="Obsolete"/> verb the catalogue could only ever express unlocks, and <c>PlayerUnlockedAsset.ObsoleteOnRound</c> and
/// <c>PlayerUnlockedComponent.ObsoleteOnTurn</c> would stay permanently unwritten - the Technology system is their intended writer.
/// </remarks>
public enum TechnologyGrantEffect
{
    /// <summary>
    /// Makes the target available to the Player, stamping the Round it was granted.
    /// </summary>
    Unlock = 1,

    /// <summary>
    /// Retires the target, stamping the Round its availability ends.
    /// </summary>
    Obsolete = 2
}
