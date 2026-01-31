
namespace AscendantEmpire.FrontendContracts.Turns.GetTurns;

public interface IRosterEntryCollection
{
    IReadOnlyCollection<IRosterEntry> RosterEntries { get; }
}
