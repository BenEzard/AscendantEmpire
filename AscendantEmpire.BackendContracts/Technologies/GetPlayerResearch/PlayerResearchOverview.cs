namespace AscendantEmpire.BackendContracts.Technologies.GetPlayerResearch;

/// <summary>
/// Where one Player's research stands: what they are working on, what they could work on, and what they have banked.
/// </summary>
/// <remarks>
/// One shape rather than several calls, because a client cannot usefully show the researchable list without also saying which of them is already the
/// project. Unlike the catalogue read this is Player-specific and changes as the game runs, so it is not cacheable for the life of a game.
/// </remarks>
public record PlayerResearchOverview
{
    /// <summary>
    /// The technology directed research is currently going into, or null when none is selected.
    /// </summary>
    /// <remarks>Null is a real state, and the one the start-of-turn "select a research project" prompt exists for.</remarks>
    public int? CurrentTechnologyId { get; init; }

    /// <summary>
    /// The current project's display name, or null alongside a null <see cref="CurrentTechnologyId"/>.
    /// </summary>
    public string? CurrentTechnologyDisplayName { get; init; }

    /// <summary>
    /// The technology to switch to automatically when the current one completes, or null when none is pre-chosen.
    /// </summary>
    public int? NextTechnologyId { get; init; }

    /// <summary>
    /// The next project's display name, or null alongside a null <see cref="NextTechnologyId"/>.
    /// </summary>
    public string? NextTechnologyDisplayName { get; init; }

    /// <summary>
    /// Directed-research points generated but not yet spent.
    /// </summary>
    /// <remarks>Zero for every Player until the end-of-turn research step exists, since nothing generates points yet.</remarks>
    public required int UnspentDirectedPoints { get; init; }

    /// <summary>
    /// Diffusion points that spilled out of a filled bucket and have not been used.
    /// </summary>
    public required int UnspentDiffusionPoints { get; init; }

    /// <summary>
    /// The Round the Game's campaign lane is on, which is what an unlock would be stamped with.
    /// </summary>
    public required int CurrentRoundNumber { get; init; }

    /// <summary>
    /// Everything the Player could research now, in era and then sort order.
    /// </summary>
    public required IReadOnlyCollection<ResearchableTechnology> Researchable { get; init; }
}
