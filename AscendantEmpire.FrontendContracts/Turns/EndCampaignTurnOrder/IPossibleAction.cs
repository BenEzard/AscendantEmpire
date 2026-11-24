using AscendantEmpire.Domain.Enums.Turns;

namespace AscendantEmpire.FrontendContracts.Turns.EndCampaignTurnOrder;

public interface IPossibleAction
{
    public InformationCategory Category { get; }
    public string Description { get; }

    /// <summary>
    /// Where on the Campaign Map the action is, when it is somewhere at all.
    /// </summary>
    /// <remarks>
    /// Null for actions that have no place on the map — choosing a research project, renewing a trade deal — so a client offering to pan to an action
    /// can tell the two apart rather than sending the player to tile (0, 0).
    /// </remarks>
    public int? XCoord { get; }

    /// <inheritdoc cref="XCoord"/>
    public int? YCoord { get; }

    public int Id { get; }
    public int RecordTypeId { get; }

    /// <summary>
    /// The template the Force concerned was built from, so a client can draw its map image; null for actions about anything other than a Unit.
    /// </summary>
    public int? UnitTemplateId { get; }
}
