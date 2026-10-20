namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// The order the end-of-Campaign-Turn processes run in. Lower values run first.
/// </summary>
/// <remarks>
/// <para>Every <see cref="IEndCampaignTurnProcess"/> returns one of these from its <c>ExecutionOrder</c> rather than a bare number, so the whole
/// sequence can be read and reordered here instead of being reconstructed by opening each process in turn.</para>
/// <para>Values are spaced in bands of ten thousand so a process can be slipped between two others without renumbering anything below it. Two
/// processes must never share a value: <c>OrderBy</c> is stable, so a tie silently falls through to the order they happen to be registered in
/// <c>ApplicationStartup</c>, and the sequence stops being decided here at all.</para>
/// </remarks>
internal enum EndCampaignTurnProcessOrder
{
    /// <summary>Forces collect from the ground they hold, before anything is spent.</summary>
    Gathering = 10_000,

    /// <summary>Construction advances, which may draw on what was just gathered.</summary>
    BuildingStructure = 20_000,

    /// <summary>Forces dig in where they stand.</summary>
    Fortifying = 30_000,

    /// <summary>Attrition is applied, judged on everything the Force did this Turn — fortifying included.</summary>
    EvaluateForceOnHostileTerrain = 40_000,

    /// <summary>The Turn's research points are poured into the Player's chosen project.</summary>
    /// <remarks>
    /// Position carries no dependency either way - research is an empire-wide book, and nothing before it in this pipeline moves a point. It sits after the
    /// Force processes simply so the ledger is closed once everything that happened to the Player's Forces this Turn has happened.
    /// </remarks>
    ApplyResearch = 45_000,

    /// <summary>What other Players watched during the Turn is written down last of all.</summary>
    /// <remarks>
    /// Deliberately after <see cref="EvaluateForceOnHostileTerrain"/>. A Force seen at sequence 3 and then destroyed by attrition was still seen, and its history rows survive the delete, so it is
    /// reported - and the reports see every destruction the Turn is going to have.
    /// </remarks>
    RaiseSightingReports = 50_000,
}
