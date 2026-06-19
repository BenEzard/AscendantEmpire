using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Per-player unlock/obsolescence state for a global (common) <see cref="UnitTemplate"/>.
/// </summary>
/// <remarks>
/// <para>Mirrors the existing prototype <c>PlayerUnitDesignState</c> shape.</para>
/// <para>A global template becomes unlockable once the player has unlocked its body component and every
/// component fitted into it. Custom (player-owned) templates are built from already-unlocked components
/// and do not need rows here. Composite primary key on (PlayerId, UnitTemplateId).</para>
/// </remarks>
public class PlayerUnlockedTemplate
{
    public int PlayerId { get; set; }
    public int UnitTemplateId { get; set; }

    /// <summary>
    /// The turn on which this template became unlocked for the player.
    /// </summary>
    public int UnlockedOnTurn { get; set; }

    /// <summary>
    /// The turn on which this template became obsolete for the player; null while still current.
    /// </summary>
    public int? ObsoleteOnTurn { get; set; }

    public bool HasBeenPrototyped { get; set; }

    public Player Player { get; set; } = null!;
    public UnitTemplate UnitTemplate { get; set; } = null!;
}
