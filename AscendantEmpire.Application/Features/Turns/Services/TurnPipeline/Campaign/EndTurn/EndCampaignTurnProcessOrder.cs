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
    Gathering = 10000,

    /// <summary>Construction advances, which may draw on what was just gathered.</summary>
    BuildingStructure = 20000,

    /// <summary>Forces dig in where they stand.</summary>
    Fortifying = 30000,

    /// <summary>Attrition is applied last, judged on everything the Force did this Turn — fortifying included.</summary>
    EvaluateForceOnHostileTerrain = 40000,
}
