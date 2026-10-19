using AscendantEmpire.Domain.Enums.Settlements;

namespace AscendantEmpire.FrontendContracts.ForceOrders.SettleForce;

/// <summary>
/// What a settle order produced: the new Settlement, and the Structure now standing where the Force was.
/// </summary>
/// <remarks>
/// <para>
/// No map payload, unlike <c>IStepForceResult</c>. Settling changes the map in two ways at once — a Force disappears and a Structure appears, and the Structure may see
/// further than the Force did — so the client refetches its view rather than being handed a patch to merge. A patch would have to describe a removal as well as an
/// addition, which the observer shape has no way to say.
/// </para>
/// <para>
/// The settlement comes back incomplete: founding starts construction rather than finishing it, so <see cref="CompletionPercent"/> is 0 and the settlement is not yet
/// standing in any sense the rest of the game cares about.
/// </para>
/// </remarks>
public interface ISettleForceResult
{
    /// <summary>The new Settlement's id.</summary>
    int SettlementId { get; }

    /// <summary>The Structure standing on the tile for the new Settlement.</summary>
    int StructureInstanceId { get; }

    /// <summary>The settlement's name as stored — trimmed, so it may differ from what was sent — or null for a tier that carries no name.</summary>
    string? Name { get; }

    /// <summary>The tier founded.</summary>
    SettlementTier Tier { get; }

    /// <summary>How many people the consumed Force brought with it.</summary>
    int Population { get; }

    /// <summary>The tile the settlement stands on.</summary>
    int XCoord { get; }

    /// <summary>The tile the settlement stands on.</summary>
    int YCoord { get; }

    /// <summary>How far built it is, from 0 to 100. Always 0 straight after founding.</summary>
    double CompletionPercent { get; }

    /// <summary>
    /// How many more of the player's Turns construction will take.
    /// </summary>
    /// <remarks>Read from the Structure catalogue, so the dialog can say "ready in 2 turns" without knowing the catalogue itself.</remarks>
    int TurnsToComplete { get; }
}
