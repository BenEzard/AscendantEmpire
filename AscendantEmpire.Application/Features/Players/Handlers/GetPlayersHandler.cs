using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Players.GetPlayers;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Players.Handlers;

public record GetPlayersRequest(
    int GameId,
    IEnumerable<PlayerStatus>? PlayerStatuses = null,
    IEnumerable<int>? PlayerIds = null
) : IRequest<ServiceResult<IReadOnlyCollection<Player>>>;

internal class GetPlayersRequestValidator : Validator<GetPlayersRequest>
{
    public GetPlayersRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0)
            .WithMessage("Game Id is required.");

        RuleForEach(x => x.PlayerIds)
            .GreaterThan(0)
            .When(x => x.PlayerIds is not null)
            .WithMessage("Player Ids must be greater than 0.");
    }
}

internal class GetPlayersHandler(
    ILogger<GetPlayersHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetPlayersRequest, ServiceResult<IReadOnlyCollection<Player>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<Player>>> Handle(GetPlayersRequest request, CancellationToken cancelToken)
    {
        try
        {
            var validator = new GetPlayersRequestValidator();
            var validationResult = validator.Validate(request);

            if (validationResult.IsValid == false)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetPlayersRequest)}.";

                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

                return ServiceResult<IReadOnlyCollection<Player>>.Fail(errors.Prepend(error));
            }

            int[]? playerIds = request.PlayerIds?.Distinct().ToArray();

            PlayerStatus[]? playerStatuses = request.PlayerStatuses?.Distinct().ToArray();

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            IQueryable<Player> query = context.Players
                .Where(p => p.GameId == request.GameId)
                .Where(p => playerIds == null || playerIds.Contains(p.Id))
                .Where(p => playerStatuses == null || playerStatuses.Contains(p.Status))
                .Select(p => new Player
                {
                    Id = p.Id,
                    GameId = p.GameId,
                    RulerTitle = p.RulerTitle,
                    RulerName = p.RulerName,
                    FactionName = p.FactionName,
                    FactionColour = p.FactionColour,
                    Status = p.Status,
                    CampaignOffsetId = p.CampaignOffsetId
                });

            List<Player> players = await query.ToListAsync(cancelToken);

            return ServiceResult<IReadOnlyCollection<Player>>.Succeed(players);
        }
        catch (Exception ex)
        {
            const string error = "Unhandled exception getting Players.";

            logger.LogError(ex, error);

            return ServiceResult<IReadOnlyCollection<Player>>.Fail([error, ex.Message]);
        }
    }
}