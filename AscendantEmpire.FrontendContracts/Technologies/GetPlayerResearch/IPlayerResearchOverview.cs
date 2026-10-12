namespace AscendantEmpire.FrontendContracts.Technologies.GetPlayerResearch;

/// <summary>
/// Where one Player's research stands: what they are working on, what they could work on, and what they have banked.
/// </summary>
/// <remarks>
/// Every member of every interface in this family must be declared on the interface, not only on the DT class implementing it. The nested collection is
/// typed by its interface, and System.Text.Json serialises collection elements by their declared type - a property missing from an interface is silently
/// dropped from the response.
/// </remarks>
public interface IPlayerResearchOverview
{
    /// <summary>
    /// The technology directed research is currently going into, or null when none is selected.
    /// </summary>
    int? CurrentTechnologyId { get; }

    string? CurrentTechnologyDisplayName { get; }

    /// <summary>
    /// The technology to switch to automatically when the current one completes, or null when none is pre-chosen.
    /// </summary>
    int? NextTechnologyId { get; }

    string? NextTechnologyDisplayName { get; }

    int UnspentDirectedPoints { get; }

    int UnspentDiffusionPoints { get; }

    /// <summary>
    /// The Round the Game's campaign lane is on.
    /// </summary>
    int CurrentRoundNumber { get; }

    /// <summary>
    /// Everything the Player could research now, in era and then sort order.
    /// </summary>
    IReadOnlyCollection<IResearchableTechnology> Researchable { get; }
}
