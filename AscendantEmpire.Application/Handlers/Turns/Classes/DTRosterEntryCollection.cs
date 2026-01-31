using AscendantEmpire.FrontendContracts.Turns.GetTurns;

namespace AscendantEmpire.Application.Handlers.Turns.Classes;

public class DTRosterEntryCollection : IRosterEntryCollection
{
    public required IReadOnlyCollection<IRosterEntry> RosterEntries { get; set; }
}
