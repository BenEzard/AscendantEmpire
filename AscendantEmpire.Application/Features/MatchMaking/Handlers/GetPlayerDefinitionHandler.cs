using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.MatchMaking;
using AscendantEmpire.Domain.Entities.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.MatchMaking.Handlers;

/// <summary>
/// Reads back the identity a user has given their own player slot in one game, along with the faction colours their rivals already hold.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="UserId"/> comes from the caller's bearer token, and the slot is resolved from it rather than named by the request, so this can
/// only ever describe the caller's own player.
/// </para>
/// <para>
/// The used-colour list is taken here rather than derived from the lobby listing the frontend already holds: that listing is cached client-side, and
/// a stale reading would offer a colour that has since been taken.
/// </para>
/// </remarks>
public record GetPlayerDefinitionRequest(int UserId, int GameId) : IRequest<ServiceResult<PlayerDefinition>>;

internal class GetPlayerDefinitionRequestValidator : Validator<GetPlayerDefinitionRequest>
{
    public GetPlayerDefinitionRequestValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User Id is required.");

        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");
    }
}

internal sealed class GetPlayerDefinitionHandler(
    ILogger<GetPlayerDefinitionHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetPlayerDefinitionRequest, ServiceResult<PlayerDefinition>>
{
    public async Task<ServiceResult<PlayerDefinition>> Handle(GetPlayerDefinitionRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // A game holds at most a handful of slots, so pull them all in one query and split them
            // in memory rather than making a second round trip for the rivals' colours.
            List<Player> slots = await context.Players
                .AsNoTracking()
                .Where(p => p.GameId == request.GameId)
                .ToListAsync(cancelToken);

            Player? ownSlot = slots.FirstOrDefault(p => p.UserId == request.UserId);

            if (ownSlot is null)
            {
                logger.LogInformation("User {UserId} asked for their player definition in game {GameId} but controls no slot there.", request.UserId, request.GameId);
                return ServiceResult<PlayerDefinition>.NotFound("You don't control a player in that game.");
            }

            List<UsedFactionColour> usedColours = [.. slots
                .Where(p => p.Id != ownSlot.Id && string.IsNullOrWhiteSpace(p.FactionColour) == false)
                .Select(p => new UsedFactionColour
                {
                    Value = p.FactionColour!,
                    UsedBy = p.FactionName
                })
                .OrderBy(c => c.UsedBy)];

            PlayerDefinition definition = new()
            {
                PlayerId = ownSlot.Id,
                GameId = ownSlot.GameId,
                RulerTitle = ownSlot.RulerTitle,
                RulerName = ownSlot.RulerName,
                RulerGender = ownSlot.RulerGender,
                FactionName = ownSlot.FactionName,
                FactionColour = ownSlot.FactionColour,
                UsedFactionColours = usedColours
            };

            return ServiceResult<PlayerDefinition>.Succeed(definition);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while fetching the player definition.";
            logger.LogError(ex, error);
            return ServiceResult<PlayerDefinition>.InternalServerError(error);
        }
    }
}
