using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.MatchMaking;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using System.Text.RegularExpressions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.MatchMaking.Handlers;

/// <summary>
/// Writes the ruler and faction identity a user has chosen for the player slot they control in one game, and starts the game once every player has
/// one.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="UserId"/> comes from the caller's bearer token and the slot is resolved from it, so a caller can only ever define their own
/// player. A caller who controls no slot in the game gets a 404 rather than a 403 - there is nothing there for them to be forbidden from.
/// </para>
/// <para>
/// <b>Several values are unique within the game.</b> The names a player authors, plus the faction colour and, once chosen, the ruler
/// portrait. Each has a unique index behind it, and the checks here exist to turn those races into friendly
/// errors; every clash is reported in one answer rather than one per resubmission. <see cref="Language"/> is deliberately exempt, because two
/// peoples sharing a language is a legitimate thing to express.
/// </para>
/// <para>
/// <b>The definition locks when play begins.</b> While the game is still awaiting players every field is writable; from
/// <see cref="GamePlayStatus.InProgress"/> onward only <paramref name="RulerTitle"/> is, and the rest of the request is ignored rather than refused.
/// The client round-trips the whole definition and renders the locked fields disabled, so an ignored field is never a change the player meant to
/// make - where refusing would turn a legal title edit into an error over values the caller only echoed back.
/// </para>
/// <para>
/// <b>The last definition to land starts the game</b>, not the last seat to be claimed: a claimed slot nobody has filled the form in for has no
/// identity to play as. Readiness is "every active slot has a user and a completed definition", and the save and the start share one transaction for
/// the same reason the claim once did - a start that failed must not leave the game ready and never begun.
/// </para>
/// <para>
/// A blank <paramref name="RulerTitle"/> is legal and stored as <see cref="Player.DEFAULT_RULER_TITLE"/> - the title is optional in the UI, and
/// defaulting it here keeps the placeholder in one place rather than spreading it into a database default. That still holds after the lock: a player
/// clearing their title mid-game is a legal edit.
/// </para>
/// <para>
/// A blank <paramref name="RulerPortraitPath"/> is legal in the same way, and stored as <see cref="Player.DEFAULT_RULER_PORTRAIT_PATH"/>. The default
/// is exempt from the uniqueness rule, so any number of players may be undecided at once.
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
    string Language,
    string RulerPortraitPath
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

        // The people names are required in the same way the faction's own name is: free text the
        // player authors, checked only for presence and length because nothing is ever looked up by them.
        RuleFor(x => x.PeopleName)
            .NotEmpty().WithMessage("People Name is required.")
            .MaximumLength(Player.PEOPLE_NAME_MAX_LENGTH).WithMessage($"People Name cannot be longer than {Player.PEOPLE_NAME_MAX_LENGTH} characters.");

        RuleFor(x => x.Demonym)
            .NotEmpty().WithMessage("Demonym is required.")
            .MaximumLength(Player.DEMONYM_MAX_LENGTH).WithMessage($"Demonym cannot be longer than {Player.DEMONYM_MAX_LENGTH} characters.");

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage("Language is required.")
            .MaximumLength(Player.LANGUAGE_MAX_LENGTH).WithMessage($"Language cannot be longer than {Player.LANGUAGE_MAX_LENGTH} characters.");

        // Deliberately no NotEmpty, like the ruler title above: an unchosen portrait is a valid choice, and the
        // handler stores the default for it. A value that *is* given is checked for shape as well as length, unlike
        // the names above, because it is rendered straight into an <img src> that every other player in the game sees
        // in their toolbar - an off-site URL from a hostile client would pull a remote image onto their screen. The
        // twenty portrait paths themselves are deliberately not enumerated here: that would mean keeping a second copy
        // of the UI's manifest in C# forever, the same reason the faction colour is checked for hex shape rather than
        // against the palette offered.
        RuleFor(x => x.RulerPortraitPath)
            .MaximumLength(Player.RULER_PORTRAIT_PATH_MAX_LENGTH).WithMessage($"Ruler Portrait cannot be longer than {Player.RULER_PORTRAIT_PATH_MAX_LENGTH} characters.")
            .Matches(@"^/portraits/[a-z0-9_-]+\.png$").WithMessage("Ruler Portrait must be one of the portraits the game offers.")
            .When(x => string.IsNullOrWhiteSpace(x.RulerPortraitPath) == false);
    }
}

internal sealed class UpdatePlayerDefinitionHandler(
    ILogger<UpdatePlayerDefinitionHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ICampaignRoundService campaignRoundService
) : IRequestHandler<UpdatePlayerDefinitionRequest, ServiceResult<Empty>>
{
    /// <summary>
    /// Matches a run of one or more whitespace characters, for collapsing the middle of a name down to single spaces.
    /// </summary>
    /// <remarks>
    /// Compiled because every definition save runs it once per unique name, and the pattern never changes.
    /// </remarks>
    private static readonly Regex _whitespaceRuns = new(@"\s+", RegexOptions.Compiled);

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
            string rulerPortraitPath = string.IsNullOrWhiteSpace(request.RulerPortraitPath)
                ? Player.DEFAULT_RULER_PORTRAIT_PATH
                : request.RulerPortraitPath.Trim();

            // The unique names get the fuller treatment: trimmed and with internal runs of whitespace collapsed, so
            // that "The  Vashti" cannot sit alongside "The Vashti" as a second name. See NormaliseName.
            string rulerName = NormaliseName(request.RulerName);
            string factionName = NormaliseName(request.FactionName);
            string peopleName = NormaliseName(request.PeopleName);
            string demonym = NormaliseName(request.Demonym);

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // A game holds at most a handful of slots, so pull them all in one query: the caller's own is in there, and
            // the rest are exactly what the uniqueness checks and the readiness test need.
            List<Player> slots = await context.Players
                .Where(p => p.GameId == request.GameId)
                .ToListAsync(cancelToken);

            // Resolved by (game, user) rather than by a player id in the request, so the caller
            // cannot name a slot that isn't theirs - hence a 404 here and no forbidden case.
            Player? slot = slots.FirstOrDefault(p => p.UserId == request.UserId);

            if (slot is null)
            {
                logger.LogInformation("User {UserId} tried to define a player in game {GameId} but controls no slot there.", request.UserId, request.GameId);
                return ServiceResult<Empty>.NotFound("You don't control a player in that game.");
            }

            GamePlayStatus playStatus = await context.GameIndex
                .Where(g => g.Id == request.GameId)
                .Select(g => g.PlayStatus)
                .FirstAsync(cancelToken);

            // From the moment play begins, the identity is what everything else labels and attributes by, so only the
            // title stays editable. The rest of the request is read no further - see the remarks on the request type for
            // why that is quieter than refusing it.
            if (playStatus is GamePlayStatus.InProgress or GamePlayStatus.Completed or GamePlayStatus.Abandoned)
            {
                slot.RulerTitle = rulerTitle;
                await context.SaveChangesAsync(cancelToken);

                logger.LogInformation("User {UserId} retitled player {PlayerId} in game {GameId}; the rest of the definition is locked at play status {PlayStatus}.",
                    request.UserId, slot.Id, request.GameId, playStatus);

                return ServiceResult<Empty>.SuccessNoContent();
            }

            List<Player> rivals = [.. slots.Where(p => p.Id != slot.Id)];

            // Every clash in one answer: a player who has picked three taken names should be told all three rather than
            // resubmit three times to find them out.
            List<PlayerDefinitionFieldConflict> clashes = [];

            CollectClash(clashes, rivals, "rulerName", "ruler name", rulerName, static p => p.RulerName);
            CollectClash(clashes, rivals, "factionName", "faction name", factionName, static p => p.FactionName);
            CollectClash(clashes, rivals, "peopleName", "people name", peopleName, static p => p.PeopleName);
            CollectClash(clashes, rivals, "demonym", "demonym", demonym, static p => p.Demonym);
            CollectClash(clashes, rivals, "factionColour", "colour", factionColour, static p => p.FactionColour ?? string.Empty);

            // The default is exempt: it is the one portrait value a game may hold more than once, because any number of
            // players can be undecided at the same time. The index behind it is filtered to match.
            if (rulerPortraitPath != Player.DEFAULT_RULER_PORTRAIT_PATH)
            {
                CollectClash(clashes, rivals, "rulerPortraitPath", "portrait", rulerPortraitPath, static p => p.RulerPortraitPath);
            }

            if (clashes.Count > 0)
            {
                logger.LogInformation("User {UserId} tried to define player {PlayerId} in game {GameId} with {ClashCount} value(s) rivals already hold: {Fields}.",
                    request.UserId, slot.Id, request.GameId, clashes.Count, string.Join(", ", clashes.Select(c => c.Field)));

                return ServiceResult<Empty>.Conflict(
                    clashes.Select(c => c.Message),
                    new PlayerDefinitionConflict { Fields = clashes });
            }

            // The save and the start it may trigger belong together. Without one transaction around both, a start that
            // failed would leave the game ready and stuck: every player defined, so nothing further would trigger a
            // start, and never begun.
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancelToken);

            slot.RulerTitle = rulerTitle;
            slot.RulerName = rulerName;
            slot.RulerGender = request.RulerGender;
            slot.FactionName = factionName;
            slot.FactionColour = factionColour;
            slot.PeopleName = peopleName;
            slot.Demonym = demonym;
            slot.Language = request.Language.Trim();
            slot.RulerPortraitPath = rulerPortraitPath;

            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation("User {UserId} defined player {PlayerId} in game {GameId}.", request.UserId, slot.Id, request.GameId);

            // Was that the last player to define themselves? Read it back rather than reasoning over the slots fetched
            // above, so anybody who defined themselves while this request was in flight is counted. A completed
            // definition is a non-null gender - see Player.RulerGender.
            bool anyPlayerNotReady = await context.Players
                .AnyAsync(p => p.GameId == request.GameId
                            && p.Status == PlayerStatus.Active
                            && (p.UserId == null || p.RulerGender == null), cancelToken);

            if (anyPlayerNotReady == false)
            {
                // Everybody is in and everybody has an identity, so play begins. The service is gated on AwaitingPlayers
                // and idempotent, so a player re-saving their definition after the game has begun cannot start it twice.
                ServiceResultBase startResult = await campaignRoundService.StartGameAsync(context, request.GameId, cancelToken);

                if (startResult.HasSucceeded == false)
                {
                    // Rolling back un-defines the player too, which is what makes re-saving the definition a real fix
                    // rather than a second failure.
                    logger.LogError("Defining the last player in game {GameId} failed to start it, so the definition was rolled back. Errors: {Errors}",
                        request.GameId, string.Join("; ", startResult.Errors));

                    return ServiceResult<Empty>.InternalServerError("Your ruler was saved but the game could not be started. Please try again.");
                }
            }

            await transaction.CommitAsync(cancelToken);

            return ServiceResult<Empty>.SuccessNoContent();
        }
        catch (DbUpdateException ex)
        {
            // Lost a race: a rival took one of the unique values between our checks and the save. The indexes are the
            // real guarantee; the checks above only make the common case friendlier, and cannot say which one it was.
            logger.LogInformation(ex, "Concurrent player definition conflict in game {GameId} for user {UserId}.", request.GameId, request.UserId);
            return ServiceResult<Empty>.Conflict("Another player in this game took one of those values while you were choosing. Please try again.");
        }
    }

    /// <summary>
    /// Trims a name and collapses every run of internal whitespace to a single space, so what gets stored is the canonical form of what was typed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Case is left to the database collation, which <see cref="Player.IDENTITY_COLLATION"/> pins case-insensitive on these columns. Spacing is not
    /// something a collation settles - "The  Vashti" with a double space is a distinct string under any of them, and a leading space counts even though
    /// SQL Server ignores a trailing one - so it has to be settled here instead.
    /// </para>
    /// <para>
    /// This normalises the real column rather than a shadow of it, so what is stored, what is displayed and what is compared are all the same string.
    /// </para>
    /// </remarks>
    private static string NormaliseName(string value)
    {
        return _whitespaceRuns.Replace(value.Trim(), " ");
    }

    /// <summary>
    /// Records a clash when a rival in the same game already holds <paramref name="requestedValue"/> in the same field.
    /// </summary>
    /// <param name="clashes">The list being accumulated, appended to only when there is something to report.</param>
    /// <param name="rivals">Every other slot in the game. The caller's own is excluded, so keeping a value they already hold is not a clash.</param>
    /// <param name="field">The field as the request body spells it, e.g. "peopleName".</param>
    /// <param name="label">How to name the field in the message shown to the player, e.g. "people name".</param>
    /// <param name="requestedValue">The normalised value the caller is asking for.</param>
    /// <param name="readRival">Reads the same field off a rival's slot.</param>
    /// <remarks>
    /// Compared here with <see cref="StringComparison.OrdinalIgnoreCase"/> rather than in the query. The slots were fetched anyway to resolve the
    /// caller's own, a game holds a handful of them, and comparing in memory behaves the same whichever provider is underneath - where a translated
    /// <c>==</c> is case-insensitive against SQL Server's collation but ordinal, and so case-sensitive, under the in-memory provider the tests use.
    /// </remarks>
    private static void CollectClash(
        List<PlayerDefinitionFieldConflict> clashes,
        IReadOnlyCollection<Player> rivals,
        string field,
        string label,
        string requestedValue,
        Func<Player, string> readRival)
    {
        bool taken = rivals.Any(p => string.Equals(readRival(p), requestedValue, StringComparison.OrdinalIgnoreCase));

        if (taken == false)
        {
            return;
        }

        clashes.Add(new PlayerDefinitionFieldConflict
        {
            Field = field,
            Message = $"That {label} has already been taken by another player in this game."
        });
    }
}
