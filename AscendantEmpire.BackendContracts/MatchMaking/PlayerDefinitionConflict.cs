namespace AscendantEmpire.BackendContracts.MatchMaking;

/// <summary>
/// The detail accompanying a 409 from a player-definition save: every value another player in that game has already taken, named field by field.
/// </summary>
/// <remarks>
/// <para>
/// Travels in the error body's <c>Detail</c> field alongside the human-readable messages. The messages are what gets shown; this is what lets the
/// setup dialog put each one on the field it belongs to, rather than matching the message text to work out which value clashed.
/// </para>
/// <para>
/// Every clash is reported together rather than one at a time, so a player who picks three taken names is told all three instead of resubmitting
/// three times.
/// </para>
/// </remarks>
public record PlayerDefinitionConflict
{
    /// <summary>The clashing values, one entry per field. Never empty - a save with nothing to report succeeds instead.</summary>
    public required IReadOnlyCollection<PlayerDefinitionFieldConflict> Fields { get; init; }
}

/// <summary>
/// One field of a player definition whose requested value is already held by another player in the same game.
/// </summary>
public record PlayerDefinitionFieldConflict
{
    /// <summary>
    /// The field that clashed, named as the request body spells it, e.g. "peopleName".
    /// </summary>
    /// <remarks>
    /// The request's own camelCased name rather than the entity's property name, so a client can key straight off the value it sent without
    /// translating. The serializer camelCases property names but not string values, so this has to be written in that casing to begin with.
    /// </remarks>
    public required string Field { get; init; }

    /// <summary>The human-readable account of this one clash, as it also appears in the error body's message list.</summary>
    public required string Message { get; init; }
}
