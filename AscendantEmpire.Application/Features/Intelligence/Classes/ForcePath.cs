using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// What kind of moment in a Force's Turn a <see cref="ForceStep"/> is.
/// </summary>
internal enum ForceStepKind
{
    /// <summary>
    /// Where the Force stood as the Turn opened, before its first move. Synthesised from that move's <c>FromCampaignMapTileId</c>; there is no history row of its own.
    /// </summary>
    Origin,

    /// <summary>
    /// The Force came into existence on the step's tile.
    /// </summary>
    Created,

    /// <summary>
    /// The Force stepped onto the step's tile.
    /// </summary>
    Moved,

    /// <summary>
    /// The Force ceased to exist on the step's tile.
    /// </summary>
    Destroyed,
}

/// <summary>
/// One moment in a Force's Turn where it could have been seen: a tile, and when in the Turn it was there.
/// </summary>
internal sealed record ForceStep
{
    /// <summary>
    /// The witness probe key for this step, unique across a whole Turn.
    /// </summary>
    /// <remarks>
    /// The history row's id for a real step, and the complement (<c>~id</c>, which is <c>-id - 1</c>) of the Force's first move for its <see cref="ForceStepKind.Origin"/>. A real step's key is
    /// never negative and a synthesised one always is, so the two ranges cannot meet - not even for a row that somehow carries id 0, where a plain negation would collide with itself.
    /// </remarks>
    public required long ProbeKey { get; init; }

    public required ForceStepKind Kind { get; init; }

    public required int CampaignMapTileId { get; init; }

    public required Coord Location { get; init; }

    /// <summary>
    /// When in the Turn the Force was on the tile. 0 for the <see cref="ForceStepKind.Origin"/>, which is before anything in the Turn happened.
    /// </summary>
    public required int TurnSequence { get; init; }
}

/// <summary>
/// One Force's whole Turn, as an ordered list of the moments it could have been seen.
/// </summary>
/// <remarks>
/// <para>
/// Built purely from the Turn's <see cref="MovementHistory"/> rows, never from the Force itself, which may have been destroyed before the sightings step runs.
/// </para>
/// <para>
/// <b>The origin step.</b> When a Force's first row in the Turn is a move, where it stepped off becomes step 0. Without it a Force that had been standing in another Player's view since last Round
/// and walked out of it this Turn would never be reported, and that Player could never be told which way it went.
/// </para>
/// </remarks>
internal sealed record ForcePath
{
    public required int UnitInstanceId { get; init; }

    /// <summary>
    /// Who owned the Force, as its latest row in the Turn records.
    /// </summary>
    public required int OwnerPlayerId { get; init; }

    public required int UnitTemplateId { get; init; }

    /// <summary>
    /// The Force's steps in the order they happened. Never empty.
    /// </summary>
    public required IReadOnlyList<ForceStep> Steps { get; init; }

    /// <summary>
    /// Builds a path for every Force the rows mention, in Force id order.
    /// </summary>
    /// <param name="rows">The history rows of one Turn, in any order.</param>
    internal static IReadOnlyList<ForcePath> BuildAll(IEnumerable<MovementHistory> rows, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(geometry);

        return [.. rows.GroupBy(row => row.UnitInstanceId).OrderBy(group => group.Key).Select(group => Build(group, geometry))];
    }

    /// <summary>
    /// Builds the path of one Force from its history rows for one Turn.
    /// </summary>
    /// <param name="rowsForOneForce">Every row the Turn holds for the Force, in any order.</param>
    /// <exception cref="ArgumentException">There are no rows, or they belong to more than one Force.</exception>
    internal static ForcePath Build(IEnumerable<MovementHistory> rowsForOneForce, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(rowsForOneForce);
        ArgumentNullException.ThrowIfNull(geometry);

        // Ordered by sequence, then by row id. Forces created together share a sequence, but one Force never has two rows at one sequence; the id is only a stable tie-break.
        List<MovementHistory> orderedRows = [.. rowsForOneForce.OrderBy(row => row.TurnSequence).ThenBy(row => row.Id)];

        if (orderedRows.Count == 0)
        {
            throw new ArgumentException("A Force's path needs at least one history row.", nameof(rowsForOneForce));
        }

        if (orderedRows.Select(row => row.UnitInstanceId).Distinct().Count() > 1)
        {
            throw new ArgumentException("Every row of a Force's path must belong to the same Force.", nameof(rowsForOneForce));
        }

        List<ForceStep> steps = [];

        MovementHistory firstRow = orderedRows[0];

        // A Force whose Turn begins with a move was standing somewhere before it - that ground is the origin step. A Turn that begins with a creation has no before.
        if (firstRow.EventType == ForceHistoryEventType.Moved && firstRow.FromCampaignMapTileId is int originTileId)
        {
            steps.Add(new ForceStep
            {
                // The complement rather than the negation: it is negative for every row id, id 0 included, so it can never be mistaken for a real step's key.
                ProbeKey = ~firstRow.Id,
                Kind = ForceStepKind.Origin,
                CampaignMapTileId = originTileId,
                Location = geometry.GetCoord(originTileId),
                TurnSequence = 0
            });
        }

        foreach (MovementHistory row in orderedRows)
        {
            steps.Add(new ForceStep
            {
                ProbeKey = row.Id,
                Kind = ToStepKind(row.EventType),
                CampaignMapTileId = row.CampaignMapTileId,
                Location = geometry.GetCoord(row.CampaignMapTileId),
                TurnSequence = row.TurnSequence
            });
        }

        // The latest row speaks for who owned the Force and what it was. They cannot change within a Turn today, but if capture ever arrives, the most recent answer is the one to report.
        MovementHistory lastRow = orderedRows[^1];

        return new ForcePath
        {
            UnitInstanceId = lastRow.UnitInstanceId,
            OwnerPlayerId = lastRow.PlayerId,
            UnitTemplateId = lastRow.UnitTemplateId,
            Steps = steps
        };
    }

    private static ForceStepKind ToStepKind(ForceHistoryEventType eventType)
    {
        // An event type with no step kind is a wiring fault in a new enum member, not a history to be skipped quietly.
        return eventType switch
        {
            ForceHistoryEventType.Created => ForceStepKind.Created,
            ForceHistoryEventType.Moved => ForceStepKind.Moved,
            ForceHistoryEventType.Destroyed => ForceStepKind.Destroyed,
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "There is no step kind for this history event type.")
        };
    }
}
