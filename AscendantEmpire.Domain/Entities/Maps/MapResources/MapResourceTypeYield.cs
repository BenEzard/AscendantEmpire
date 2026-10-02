namespace AscendantEmpire.Domain.Entities.Maps.MapResources;

/// <summary>
/// One Asset a MapResourceType gives, and how much of it one unit extracted gives, e.g. a Deer gives medium_game and a pelt.
/// </summary>
/// <remarks>
/// Not used by a Distribution or a stratified type, which hold their own Assets. Keyed on the type and the Asset.
/// </remarks>
public class MapResourceTypeYield
{
    public int MapResourceTypeId { get; set; }

    public MapResourceType MapResourceType { get; set; } = null!;

    /// <summary>
    /// The Asset given (a <c>assets.Asset</c>).
    /// </summary>
    public int AssetId { get; set; }

    /// <summary>
    /// How much of the Asset one unit extracted gives, stored scaled by <c>ScaledAmount.Scale</c> (1,000).
    /// </summary>
    public long AmountPerUnit { get; set; }

    /// <summary>
    /// Whether a Player gets this Asset before any mechanic switches it on for them.
    /// </summary>
    /// <remarks>
    /// A Deer's medium_game is on, but its pelt is off until the Player has hide-working: until then the hides are lost, though the Deer taken still come off the tile.
    /// See <c>PlayerMapResourceSwitch</c>.
    /// </remarks>
    public bool IsEnabledByDefault { get; set; }
}
