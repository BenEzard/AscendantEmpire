namespace AscendantEmpire.FrontendContracts.Units;

public interface IPlayerForce
{
    int CampaignMapTileId { get; }
    int? BattleMapTileId { get; }
    int CampaignXCoord { get; }
    int CampaignYCoord { get; }
    int SightRange { get; }
    string MapImagePath { get; }

    /// <summary>
    /// Whether this Force still has campaign movement left to spend this turn.
    /// </summary>
    /// <remarks>
    /// A display hint, never authorisation. Whether a Force may actually take an action is decided by the context-action pipeline against a freshly
    /// built context, not by this flag.
    /// </remarks>
    bool HasCampaignMovementRemaining { get; }

    /// <summary>
    /// The campaign movement points this Force has left this turn.
    /// </summary>
    /// <remarks>
    /// <para>May be negative: entering a tile costs half its movement cost but spends all of it.</para>
    /// <para>May exceed <see cref="CampaignMovementPoints"/>: unspent movement is carried into the next turn and added on top of the allowance.</para>
    /// </remarks>
    decimal CampaignMovementRemaining { get; }

    /// <summary>
    /// This Force's full movement allowance for a turn, from its compiled template.
    /// </summary>
    decimal CampaignMovementPoints { get; }

    /// <summary>
    /// The battle movement points this Force has left in its current Battle.
    /// </summary>
    /// <remarks>
    /// A separate pool from <see cref="CampaignMovementRemaining"/>. Granted when the Unit is fielded and not yet replenished by anything.
    /// </remarks>
    decimal BattleMovementRemaining { get; }
}
