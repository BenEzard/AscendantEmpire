namespace AscendantEmpire.Domain.Entities.Maps.MapResources;

/// <summary>
/// The <see cref="MapResourceType.Code"/> values the application itself has to know, as opposed to the ones only an author ever names.
/// </summary>
/// <remarks>
/// <para>
/// A MapResourceType's Code is its fixed key once saved, so code that must find one particular type has to match on the literal. Naming those literals here
/// keeps the seed data, the lookups and the tests pointing at the same string.
/// </para>
/// <para>
/// A type listed here is still ordinary catalogue data: it can be absent from a database that has not run the seed's Map Resources target, and every caller
/// has to cope with not finding it.
/// </para>
/// </remarks>
public static class MapResourceTypeCodes
{
    /// <summary>
    /// The one wood MapResourceType, covering Forest, Jungle and Woodland.
    /// </summary>
    /// <remarks>
    /// Found by this Code when a generated or repainted tile is given the Timber its Biome implies. Which Biomes those are comes from the type's habitats, not
    /// from this constant. See <c>docs/design/campaign-map/map-resources.md</c>.
    /// </remarks>
    public const string TIMBER = "Timber";
}
