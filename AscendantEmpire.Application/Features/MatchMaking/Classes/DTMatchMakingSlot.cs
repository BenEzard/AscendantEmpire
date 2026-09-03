using AscendantEmpire.FrontendContracts.MatchMaking;

namespace AscendantEmpire.Application.Features.MatchMaking.Classes;

public class DTMatchMakingSlot : IMatchMakingSlot
{
    /// <inheritdoc />
    public int PlayerId { get; set; }

    /// <inheritdoc />
    public required string RulerTitle { get; set; }

    /// <inheritdoc />
    public required string RulerName { get; set; }

    /// <inheritdoc />
    public required string FactionName { get; set; }

    /// <inheritdoc />
    public string? FactionColour { get; set; }

    /// <inheritdoc />
    public required string RulerLabel { get; set; }

    /// <inheritdoc />
    public bool IsClaimed { get; set; }

    /// <inheritdoc />
    public bool IsClaimedByCurrentUser { get; set; }
}
