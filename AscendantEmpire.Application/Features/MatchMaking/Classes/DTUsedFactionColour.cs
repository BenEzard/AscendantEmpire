using AscendantEmpire.FrontendContracts.MatchMaking;

namespace AscendantEmpire.Application.Features.MatchMaking.Classes;

public class DTUsedFactionColour : IUsedFactionColour
{
    /// <inheritdoc />
    public required string Value { get; set; }

    /// <inheritdoc />
    public required string UsedBy { get; set; }
}
