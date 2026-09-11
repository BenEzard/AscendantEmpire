namespace AscendantEmpire.Domain.Enums.Forces;

/// <summary>
/// What a <c>MovementHistory</c> row records happening to a Force.
/// </summary>
/// <remarks>
/// Numbered from 1, the same convention as <see cref="ForceOrderStatus"/>, so a value nobody set reads as no event at all rather than as a real one.
/// </remarks>
public enum ForceHistoryEventType
{
    /// <summary>
    /// The Force came into existence on the row's tile.
    /// </summary>
    Created = 1,

    /// <summary>
    /// The Force stepped off the row's <c>FromCampaignMapTileId</c> and onto its <c>CampaignMapTileId</c>.
    /// </summary>
    Moved = 2,

    /// <summary>
    /// The Force ceased to exist on the row's tile.
    /// </summary>
    Destroyed = 3
}
