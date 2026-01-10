using System.Data;

namespace AscendantEmpire.BackendContracts.CampaignMaps;

public class MapObserverOriginLocation
{
    /// <summary>
    /// Unique id which provides traceability back to the viewing entity
    /// </summary>
    public required int TemporaryObserverId { get; set; }

    /// <summary>
    /// The coordinates which the viewing entity is located at
    /// </summary>
    public required SightCoord OccupiedCoord { get; set; }

    /// <summary>
    /// The radius (number of Tiles) which the entity should be able to see if unobstructed
    /// </summary>
    public required int SightRange { get; set; }

    /// <summary>
    /// The coordinates which this location can view. In contrast to other sight-paths, the origin can see multiple children
    /// </summary>
    public List<SightCoord>? SightChildren { get; set; }

    public IReadOnlyCollection<int> GetDbIds()
    {
        int[] ids = [OccupiedCoord.DbId];

        if (SightChildren != null)
        {
            foreach (var child in SightChildren)
            {
                ids = [.. ids.Concat(Traverse(child).Select(c => c.DbId))];
            }
        }

        return [.. ids.Distinct()];
    }

    private static IEnumerable<SightCoord> Traverse(SightCoord node)
    {
        yield return node;

        if (node.Children == null)
            yield break;

        foreach (var child in node.Children)
        {
            foreach (var descendant in Traverse(child))
            {
                yield return descendant;
            }
        }
    }
}
