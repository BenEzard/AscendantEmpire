using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;

namespace AscendantEmpire.Application.Features.Turns.Services;

/// <summary>
/// Why a Player could not be given the Game's current Campaign Turn, or <see cref="Granted"/> when they could.
/// </summary>
/// <remarks>
/// <para>
/// Separate members rather than one failure, because the two callers disagree about what each one means. Asking to end a Turn that is not open is a
/// refusal; reading outstanding actions for the same Game is simply an empty answer.
/// </para>
/// </remarks>
internal enum CampaignTurnAccessStatus
{
    /// <summary>The Game is in progress, the Player belongs to it, and a Campaign Turn is open. Whose Turn it is, is the caller's to judge.</summary>
    Granted = 0,

    /// <summary>The Game was not found, is not in progress, or the Player is not active in it.</summary>
    PlayerNotInGame = 1,

    /// <summary>The Game is one the Player belongs to, but nobody is currently taking a Campaign Turn in it.</summary>
    NoTurnInProgress = 2,

    /// <summary>Something went wrong reading either of the above.</summary>
    Failed = 3
}

/// <summary>
/// The answer to "may this Player act on this Game's current Campaign Turn, and what is that Turn?".
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CurrentTurn"/> is populated only for <see cref="CampaignTurnAccessStatus.Granted"/>, and is the Turn of whoever is taking it — not
/// necessarily the Player who asked. Callers compare <see cref="CurrentCampaignTurn.PlayerId"/> themselves.
/// </para>
/// </remarks>
internal sealed record CampaignTurnAccess
{
    public required CampaignTurnAccessStatus Status { get; init; }

    /// <summary>The Game's open Campaign Turn, or <see langword="null"/> for any status other than <see cref="CampaignTurnAccessStatus.Granted"/>.</summary>
    public CurrentCampaignTurn? CurrentTurn { get; init; }

    /// <summary>What went wrong, ready to be handed to a caller's own failure result. Empty when access was granted.</summary>
    public IReadOnlyCollection<string> Errors { get; init; } = [];
}
