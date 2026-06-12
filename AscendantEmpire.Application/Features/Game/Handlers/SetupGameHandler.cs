using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Domain.Entities.Games;

using FluentValidation;

using MediatR;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Game.Handlers;

public record SetupGameRequest(int GalleryMapId, string GameName, int NumberOfPlayers, int BattleMapSize, long? GenerationSeed) : IRequest<ServiceResult<int>>;

internal class SetupGameRequestValidator : AbstractValidator<SetupGameRequest>
{
    public SetupGameRequestValidator()
    {
        RuleFor(x => x.GalleryMapId)
            .GreaterThan(0)
            .WithMessage("Gallery Map Id is required.");

        RuleFor(g => g.GameName)
            .NotEmpty().WithMessage(g => $"{nameof(g.GameName)} is required.")
            .MaximumLength(GameIndex.GAME_NAME_MAX_LENGTH).WithMessage(g => $"{nameof(g.GameName)} cannot exceed {GameIndex.GAME_NAME_MAX_LENGTH} characters.");

        RuleFor(g => g.NumberOfPlayers)
            .InclusiveBetween(GameSetupService.MIN_NUMBER_OF_PLAYERS, GameSetupService.MAX_NUMBER_OF_PLAYERS)
            .WithMessage(g => $"{nameof(g.NumberOfPlayers)} must be between {GameSetupService.MIN_NUMBER_OF_PLAYERS} and {GameSetupService.MAX_NUMBER_OF_PLAYERS} players.");

        RuleFor(g => g.BattleMapSize)
            .InclusiveBetween(GameIndex.MIN_BATTLE_MAP_SIZE, GameIndex.MAX_BATTLE_MAP_SIZE)
            .WithMessage(g => $"{nameof(g.BattleMapSize)} must be between {GameIndex.MIN_BATTLE_MAP_SIZE} and {GameIndex.MAX_BATTLE_MAP_SIZE}.");
    }
}

internal class SetupGameHandler(
    IGameSetupService gameSetupService,
    ILogger<SetupGameHandler> logger
) : IRequestHandler<SetupGameRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(SetupGameRequest request, CancellationToken cancellationToken)
    {
        var validator = new SetupGameRequestValidator();
        var validationResult = validator.Validate(request);

        if (validationResult.IsValid == false)
        {
            const string error = "Validation failed for Setup Game Request.";
            logger.LogWarning(error + ": {Errors}", validationResult.Errors.Select(e => e.ErrorMessage));
            return ServiceResult<int>.Fail(validationResult.Errors.Select(e => e.ErrorMessage).Prepend(error));
        }

        return await gameSetupService.SetupGameAsync(request, cancellationToken);
    }
}

