using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.MatchMaking.Handlers;

/// <summary>
/// Writes the ruler and faction identity a user has chosen for the player slot they control in one game.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="UserId"/> comes from the caller's bearer token and the slot is resolved from it, so a caller can only ever define their own
/// player. A caller who controls no slot in the game gets a 404 rather than a 403 - there is nothing there for them to be forbidden from.
/// </para>
/// <para>
/// A faction colour is unique within a game (enforced by the <c>UX_Player_Game_FactionColour</c> filtered unique index); the explicit check here
/// just turns that race into a friendly error.
/// </para>
/// <para>
/// A blank <paramref name="RulerTitle"/> is legal and stored as <see cref="Player.DEFAULT_RULER_TITLE"/> - the title is optional in the UI, and
/// defaulting it here keeps the placeholder in one place rather than spreading it into a database default.
/// </para>
/// </remarks>
public record UpdatePlayerDefinitionRequest(
    int UserId,
    int GameId,
    string? RulerTitle,
    string RulerName,
    RulerGender RulerGender,
    string FactionName,
    string FactionColour,
    string PeopleName,
    string Demonym,
    string CultureName,
    string Language
) : IRequest<ServiceResult<Empty>>;

internal class UpdatePlayerDefinitionRequestValidator : Validator<UpdatePlayerDefinitionRequest>
{
    public UpdatePlayerDefinitionRequestValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User Id is required.");

        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        // Deliberately no NotEmpty: a blank title is a valid choice, and the handler stores the
        // default placeholder for it.
        RuleFor(x => x.RulerTitle)
            .MaximumLength(Player.RULER_TITLE_MAX_LENGTH).WithMessage($"Ruler Title cannot be longer than {Player.RULER_TITLE_MAX_LENGTH} characters.");

        RuleFor(x => x.RulerName)
            .NotEmpty().WithMessage("Ruler Name is required.")
            .MaximumLength(Player.RULER_NAME_MAX_LENGTH).WithMessage($"Ruler Name cannot be longer than {Player.RULER_NAME_MAX_LENGTH} characters.");

        RuleFor(x => x.RulerGender)
            .IsInEnum().WithMessage("Ruler Gender must be Male or Female.");

        RuleFor(x => x.FactionName)
            .NotEmpty().WithMessage("Faction Name is required.")
            .MaximumLength(Player.FACTION_NAME_MAX_LENGTH).WithMessage($"Faction Name cannot be longer than {Player.FACTION_NAME_MAX_LENGTH} characters.");

        RuleFor(x => x.FactionColour)
            .NotEmpty().WithMessage("Faction Colour is required.")
            .Matches("^#[0-9a-fA-F]{6}$").WithMessage("Faction Colour must be a hex colour such as #1d293d.");

        // The four people-and-culture names are required in the same way the faction's own name is: free text the
        // player authors, checked only for presence and length because nothing is ever looked up by them.
        RuleFor(x => x.PeopleName)
            .NotEmpty().WithMessage("People Name is required.")
            .MaximumLength(Player.PEOPLE_NAME_MAX_LENGTH).WithMessage($"People Name cannot be longer than {Player.PEOPLE_NAME_MAX_LENGTH} characters.");

        RuleFor(x => x.Demonym)
            .NotEmpty().WithMessage("Demonym is required.")
            .MaximumLength(Player.DEMONYM_MAX_LENGTH).WithMessage($"Demonym cannot be longer than {Player.DEMONYM_MAX_LENGTH} characters.");

        RuleFor(x => x.CultureName)
            .NotEmpty().WithMessage("Culture Name is required.")
            .MaximumLength(Player.CULTURE_NAME_MAX_LENGTH).WithMessage($"Culture Name cannot be longer than {Player.CULTURE_NAME_MAX_LENGTH} characters.");

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage("Language is required.")
            .MaximumLength(Player.LANGUAGE_MAX_LENGTH).WithMessage($"Language cannot be longer than {Player.LANGUAGE_MAX_LENGTH} characters.");
    }
}

internal sealed class UpdatePlayerDefinitionHandler(
    ILogger<UpdatePlayerDefinitionHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<UpdatePlayerDefinitionRequest, ServiceResult<Empty>>
{
    public async Task<ServiceResult<Empty>> Handle(UpdatePlayerDefinitionRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Normalise before anything compares or stores these. Lower-casing the colour makes the
            // clash check independent of the database collation, which matters because SQL Server
            // compares case-insensitively and the in-memory provider used by tests does not.
            string factionColour = request.FactionColour.Trim().ToLowerInvariant();
            string rulerTitle = string.IsNullOrWhiteSpace(request.RulerTitle)
                ? Player.DEFAULT_RULER_TITLE
                : request.RulerTitle.Trim();

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Resolved by (game, user) rather than by a player id in the request, so the caller
            // cannot name a slot that isn't theirs - hence a 404 here and no forbidden case.
            Player? slot = await context.Players
                .FirstOrDefaultAsync(p => p.GameId == request.GameId && p.UserId == request.UserId, cancelToken);

            if (slot is null)
            {
                logger.LogInformation("User {UserId} tried to define a player in game {GameId} but controls no slot there.", request.UserId, request.GameId);
                return ServiceResult<Empty>.NotFound("You don't control a player in that game.");
            }

            bool colourTaken = await context.Players
                .AnyAsync(p => p.GameId == request.GameId && p.Id != slot.Id && p.FactionColour == factionColour, cancelToken);

            if (colourTaken)
            {
                logger.LogInformation("User {UserId} tried to take colour {FactionColour} in game {GameId}, but another player already holds it.", request.UserId, factionColour, request.GameId);
                return ServiceResult<Empty>.Conflict("That colour has already been taken by another player in this game.");
            }

            slot.RulerTitle = rulerTitle;
            slot.RulerName = request.RulerName.Trim();
            slot.RulerGender = request.RulerGender;
            slot.FactionName = request.FactionName.Trim();
            slot.FactionColour = factionColour;
            slot.PeopleName = request.PeopleName.Trim();
            slot.Demonym = request.Demonym.Trim();
            slot.CultureName = request.CultureName.Trim();
            slot.Language = request.Language.Trim();

            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation("User {UserId} defined player {PlayerId} in game {GameId}.", request.UserId, slot.Id, request.GameId);

            return ServiceResult<Empty>.SuccessNoContent();
        }
        catch (DbUpdateException ex)
        {
            // Lost a race: another player took the colour between our check and the save. The unique
            // index is the real guarantee; the check above only makes the common case friendlier.
            logger.LogInformation(ex, "Concurrent faction colour conflict in game {GameId} for user {UserId}.", request.GameId, request.UserId);
            return ServiceResult<Empty>.Conflict("That colour has already been taken by another player in this game.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(UpdatePlayerDefinitionRequest));
            return ServiceResult<Empty>.InternalServerError("An unexpected error occurred while saving the player definition.");
        }
    }
}
