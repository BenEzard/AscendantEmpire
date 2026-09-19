namespace AscendantEmpire.Domain.Enums.MapResources;

/// <summary>
/// How a MapResource is extracted from its tile.
/// </summary>
/// <remarks>
/// Each <c>MapResourceType</c> has exactly one. Extraction itself is not modelled yet (see "MapResourceTypes" in <c>docs/design/campaign-map/map-resources.md</c>), but whatever
/// extracts will need to know which kind of work it is: an improvement or a Force that can hunt cannot necessarily mine.
/// </remarks>
public enum ExtractionMethod
{
    /// <summary>
    /// Picked by hand, e.g. berries.
    /// </summary>
    Gather = 1,

    /// <summary>
    /// Killed and butchered, e.g. deer.
    /// </summary>
    Hunt = 2,

    /// <summary>
    /// Cut down, e.g. a forest's woods.
    /// </summary>
    Fell = 3,

    /// <summary>
    /// Dug out, e.g. a coal seam.
    /// </summary>
    Mine = 4,

    /// <summary>
    /// Taken alive, e.g. horses. Only succeeds with the type's <c>CaptureLikelihood</c>, but always reduces the quantity on the tile.
    /// </summary>
    Capture = 5
}
