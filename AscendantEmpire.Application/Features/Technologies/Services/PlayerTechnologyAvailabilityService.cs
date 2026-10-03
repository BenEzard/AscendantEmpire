using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <inheritdoc cref="IPlayerTechnologyAvailabilityService"/>
internal sealed class PlayerTechnologyAvailabilityService(
    ILogger<PlayerTechnologyAvailabilityService> logger
    ) : IPlayerTechnologyAvailabilityService
{
    public async Task<int> ReconcileAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        CancellationToken cancellationToken)
    {
        TechnologyGraph graph = await LoadGraphAsync(context, cancellationToken);

        List<PlayerTechnology> rows = await context.PlayerTechnologies
            .Where(row => row.PlayerId == playerId)
            .ToListAsync(cancellationToken);

        return Reconcile(context, graph, gameId, playerId, rows);
    }

    public async Task<int> ReconcileGameAsync(
        AscendantEmpireContext context,
        int gameId,
        CancellationToken cancellationToken)
    {
        TechnologyGraph graph = await LoadGraphAsync(context, cancellationToken);

        int[] playerIds = await context.Players
            .Where(player => player.GameId == gameId)
            .Select(player => player.Id)
            .ToArrayAsync(cancellationToken);

        List<PlayerTechnology> rows = await context.PlayerTechnologies
            .Where(row => row.GameId == gameId)
            .ToListAsync(cancellationToken);

        int changed = 0;

        foreach (int playerId in playerIds)
        {
            changed += Reconcile(context, graph, gameId, playerId, [.. rows.Where(row => row.PlayerId == playerId)]);
        }

        return changed;
    }

    /// <summary>
    /// Backfills and recomputes one Player's rows against an already-loaded graph.
    /// </summary>
    private int Reconcile(
        AscendantEmpireContext context,
        TechnologyGraph graph,
        int gameId,
        int playerId,
        List<PlayerTechnology> rows)
    {
        Dictionary<int, PlayerTechnology> byTechnologyId = rows.ToDictionary(row => row.TechnologyId);

        int changed = 0;

        // --- Backfill ---
        // A technology seeded after this game was set up has no row for this Player at all. Its requirements come from the catalogue with no multiplier
        // applied, because the game's settings were consumed at setup and are not re-read here - which is a known limitation rather than a decision: a
        // technology added mid-game is cheaper than its neighbours by exactly the game's multiplier.
        foreach (TechnologyNode node in graph.Nodes.Values)
        {
            if (byTechnologyId.ContainsKey(node.Id))
            {
                continue;
            }

            var backfilled = new PlayerTechnology
            {
                PlayerId = playerId,
                TechnologyId = node.Id,
                GameId = gameId,
                Status = TechnologyStatus.Locked,
                DirectedRequired = node.DirectedRequired,
                ExperienceRequired = node.ExperienceRequired,
                DiffusionRequired = node.DiffusionRequired,
                DirectedPoints = 0,
                ExperiencePoints = 0,
                DiffusionPoints = 0
            };

            context.PlayerTechnologies.Add(backfilled);
            byTechnologyId[node.Id] = backfilled;
            changed++;

            logger.LogInformation(
                "Backfilled PlayerTechnology for technology {TechnologyId} and player {PlayerId}: it was seeded after the game was set up.",
                node.Id,
                playerId);
        }

        // --- Recompute ---
        HashSet<int> discovered = [.. byTechnologyId.Values
            .Where(row => row.Status == TechnologyStatus.Discovered)
            .Select(row => row.TechnologyId)];

        foreach (PlayerTechnology row in byTechnologyId.Values)
        {
            // Unlocked and Discovered are facts about what the Player did, not derivations. A technology does not become unreachable again because somebody
            // re-authored its prerequisites.
            if (row.Status is TechnologyStatus.Unlocked or TechnologyStatus.Discovered)
            {
                continue;
            }

            TechnologyStatus wanted = IsAvailable(graph, row.TechnologyId, discovered)
                ? TechnologyStatus.Available
                : TechnologyStatus.Locked;

            if (row.Status != wanted)
            {
                row.Status = wanted;
                changed++;
            }
        }

        return changed;
    }

    /// <summary>
    /// Whether a technology's prerequisites are satisfied by what the Player has discovered.
    /// </summary>
    /// <remarks>
    /// All mandatory edges must be met. Optional edges are a choice, so at least one must be met - but only where the technology has any at all, since
    /// "at least one of none" would otherwise lock everything with no optional prerequisites.
    /// </remarks>
    private static bool IsAvailable(TechnologyGraph graph, int technologyId, HashSet<int> discovered)
    {
        if (!graph.Nodes.TryGetValue(technologyId, out TechnologyNode? node))
        {
            // The technology has gone from the catalogue. Leave the orphaned row locked rather than guessing; the catalogue is the authority on what exists.
            return false;
        }

        bool everyMandatoryMet = node.Mandatory.All(discovered.Contains);
        bool anyOptionalMet = node.Optional.Count == 0 || node.Optional.Any(discovered.Contains);

        return everyMandatoryMet && anyOptionalMet;
    }

    /// <summary>
    /// Loads the whole prerequisite graph in two queries.
    /// </summary>
    /// <remarks>
    /// The catalogue is a few hundred technologies with a handful of edges each, and every Player in the game is reconciled against the same graph, so it is
    /// read once and walked in memory rather than queried per technology.
    /// </remarks>
    private static async Task<TechnologyGraph> LoadGraphAsync(AscendantEmpireContext context, CancellationToken cancellationToken)
    {
        var technologies = await context.Technologies
            .AsNoTracking()
            .Select(technology => new
            {
                technology.Id,
                technology.DirectedRequired,
                technology.ExperienceRequired,
                technology.DiffusionRequired
            })
            .ToArrayAsync(cancellationToken);

        var edges = await context.TechnologyPrerequisites
            .AsNoTracking()
            .Select(prerequisite => new
            {
                prerequisite.TechnologyId,
                prerequisite.PrerequisiteTechnologyId,
                prerequisite.PrerequisiteKind
            })
            .ToArrayAsync(cancellationToken);

        Dictionary<int, TechnologyNode> nodes = technologies.ToDictionary(
            technology => technology.Id,
            technology => new TechnologyNode(
                technology.Id,
                technology.DirectedRequired,
                technology.ExperienceRequired,
                technology.DiffusionRequired));

        foreach (var edge in edges)
        {
            if (!nodes.TryGetValue(edge.TechnologyId, out TechnologyNode? node))
            {
                continue;
            }

            if (edge.PrerequisiteKind == TechnologyPrerequisiteKind.Optional)
            {
                node.Optional.Add(edge.PrerequisiteTechnologyId);
            }
            else
            {
                node.Mandatory.Add(edge.PrerequisiteTechnologyId);
            }
        }

        return new TechnologyGraph(nodes);
    }

    private sealed record TechnologyGraph(Dictionary<int, TechnologyNode> Nodes);

    private sealed record TechnologyNode(int Id, int? DirectedRequired, int? ExperienceRequired, int? DiffusionRequired)
    {
        public List<int> Mandatory { get; } = [];

        public List<int> Optional { get; } = [];
    }
}
