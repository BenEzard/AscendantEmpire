using AscendantEmpire.FrontendContracts.Games.GetOpenGames;

namespace AscendantEmpire.Application.Features.Game.Classes;

public class DTOpenGamePlayer : IOpenGamePlayer
{
    /// <inheritdoc />
    public int PlayerId { get; set; }

    /// <inheritdoc />
    public Guid PlayerGuid { get; set; }

    /// <inheritdoc />
    public required string RulerTitle { get; set; }

    /// <inheritdoc />
    public required string RulerName { get; set; }

    /// <inheritdoc />
    public required string FactionName { get; set; }

    /// <inheritdoc />
    public required string RulerLabel { get; set; }

    /// <inheritdoc />
    public int CampaignOffsetId { get; set; }
}
