using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.Games;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Game.Handlers;

public record SetupGameRequest(int GalleryMapId, string GameName, int NumberOfPlayers, int BattleMapSize, long? GenerationSeed) : IRequest<ServiceResult<int>>;

/// <summary>
/// Validates the parts of a new-game request that are fixed rules, whatever the server is configured to allow.
/// </summary>
/// <remarks>
/// The player-count and battle-map-size bounds are deliberately absent: they come from <see cref="GameSettings"/>, and a validator is a shared
/// singleton that may hold no dependencies (see <c>ValidatorRegistration</c>). The handler checks them instead.
/// </remarks>
internal class SetupGameRequestValidator : Validator<SetupGameRequest>
{
    public SetupGameRequestValidator()
    {
        RuleFor(x => x.GalleryMapId)
            .GreaterThan(0)
            .WithMessage("Gallery Map Id is required.");

        RuleFor(g => g.GameName)
            .NotEmpty().WithMessage(g => $"{nameof(g.GameName)} is required.")
            .MaximumLength(GameIndex.GAME_NAME_MAX_LENGTH).WithMessage(g => $"{nameof(g.GameName)} cannot exceed {GameIndex.GAME_NAME_MAX_LENGTH} characters.");
    }
}

internal class SetupGameHandler(
    IGameSetupService gameSetupService,
    IOptions<GameSettings> gameSettings,
    ILogger<SetupGameHandler> logger
) : IRequestHandler<SetupGameRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(SetupGameRequest request, CancellationToken cancellationToken)
    {
        var validator = new SetupGameRequestValidator();
        var validationResult = validator.Validate(request);

        // Materialised rather than left lazy: it is read three times below, and GetConfiguredBoundsErrors is an iterator that would re-run each time.
        List<string> errors = [.. validationResult.Errors.Select(e => e.ErrorMessage), .. GetConfiguredBoundsErrors(request)];

        if (errors.Count > 0)
        {
            const string error = "Validation failed for Setup Game Request.";
            logger.LogWarning(error + ": {Errors}", errors);
            return ServiceResult<int>.Fail(errors.Prepend(error));
        }

        return await gameSetupService.SetupGameAsync(request, cancellationToken);
    }

    /// <summary>
    /// Checks the request against the bounds the server is configured to allow, reporting one message per bound it falls outside.
    /// </summary>
    /// <remarks>
    /// Here rather than in <see cref="SetupGameRequestValidator"/> because the bounds are an injected dependency, which a validator may not hold. They
    /// are the same bounds <c>GET /games/settings</c> hands the New Game dialog to build its controls from, read from the one configuration section, so
    /// the form cannot offer a value this then refuses.
    /// </remarks>
    private IEnumerable<string> GetConfiguredBoundsErrors(SetupGameRequest request)
    {
        GameSettings settings = gameSettings.Value;

        if (request.NumberOfPlayers < settings.MinimumPlayersPerGame || request.NumberOfPlayers > settings.MaximumPlayersPerGame)
        {
            yield return $"{nameof(request.NumberOfPlayers)} must be between {settings.MinimumPlayersPerGame} and {settings.MaximumPlayersPerGame} players.";
        }

        BattleMapSettings battleMapSettings = settings.BattleMapSettings;

        if (request.BattleMapSize < battleMapSettings.MinimumBattleMapSize || request.BattleMapSize > battleMapSettings.MaximumBattleMapSize)
        {
            yield return $"{nameof(request.BattleMapSize)} must be between {battleMapSettings.MinimumBattleMapSize} and {battleMapSettings.MaximumBattleMapSize}.";
        }

        // Reported alongside the bounds rather than instead of them: a size that is both out of range and off the step has two things wrong with it,
        // and naming only the first would send the caller back for a second refusal. The increment is guarded because a section that omits it binds
        // to zero, and dividing by that would throw rather than refuse the request.
        if (battleMapSettings.BattleMapSizeIncrement > 0 && request.BattleMapSize % battleMapSettings.BattleMapSizeIncrement != 0)
        {
            yield return $"{nameof(request.BattleMapSize)} must be divisible by {battleMapSettings.BattleMapSizeIncrement}.";
        }
    }
}

