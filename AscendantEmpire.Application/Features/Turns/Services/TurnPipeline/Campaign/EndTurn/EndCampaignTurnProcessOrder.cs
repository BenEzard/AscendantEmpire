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

    /// <summary>Each camp empties its store into the Settlement it feeds.</summary>
    /// <remarks>
    /// After <see cref="BuildingStructure"/> on purpose. A camp spends on its own construction out of its own bank first, so the remainder is what is swept to the
    /// parent; emptying it beforehand would send the same stock across the network and straight back for the same build order.
    /// </remarks>
    DeliverCampProduction = 25_000,

    /// <summary>Forces dig in where they stand.</summary>
    Fortifying = 30_000,

    /// <summary>Attrition is applied, judged on everything the Force did this Turn — fortifying included.</summary>
    EvaluateForceOnHostileTerrain = 40_000,

    /// <summary>The Turn's experience is distributed and its research points are poured into the Player's chosen project.</summary>
    /// <remarks>
    /// <b>It must stay after the Force and Structure processes.</b> Experience is derived from what the Turn's movement and construction wrote, so a
    /// Structure that completes in <see cref="BuildingStructure"/> has to have completed before this reads the history, and the ledger is closed once
    /// everything that happened to the Player's Forces this Turn has happened.
    /// </remarks>
    ApplyResearch = 45_000,

    /// <summary>What other Players watched during the Turn is written down last of all.</summary>
    /// <remarks>
    /// Deliberately after <see cref="EvaluateForceOnHostileTerrain"/>. A Force seen at sequence 3 and then destroyed by attrition was still seen, and its history rows survive the delete, so it is
    /// reported - and the reports see every destruction the Turn is going to have.
    /// </remarks>
    RaiseSightingReports = 50_000,
}
