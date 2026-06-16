namespace AscendantEmpire.BackendContracts.MatchMaking;

/// <summary>
/// Request body for claiming a single unassigned player slot. The claiming account is taken
/// from the bearer token, never from this body.
/// </summary>
public record class ClaimPlayerSlotDto
{
    /// <summary>The game that contains the slot.</summary>
    public required int GameId { get; init; }

    /// <summary>The player slot to claim.</summary>
    public required int PlayerId { get; init; }
}
