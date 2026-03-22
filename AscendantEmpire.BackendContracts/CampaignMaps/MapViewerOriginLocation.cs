using AscendantEmpire.Domain.Enums;

using System.Data;

namespace AscendantEmpire.BackendContracts.CampaignMaps;

public class MapObserverOriginLocation
{
    /// <summary>
    /// Unique id which provides traceability back to the viewing entity
    /// </summary>
    public required int Id { get; set; }
    public required RecordType RecordTypeId { get; set; }

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

    /// <summary>
    /// Retrieves a read-only collection of unique database IDs associated with the current node and its sight children.
    /// </summary>
    /// <returns>A read-only collection of integers containing the database IDs for the current node and all of its sight
    /// children. The collection contains no duplicate values.</returns>
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

    public static IEnumerable<SightCoord> Traverse(SightCoord node)
    {
        yield return node;

        if (node.Children == null)
        {
            yield break;
        }

        foreach (var child in node.Children)
        {
            foreach (var descendant in Traverse(child))
            {
                yield return descendant;
            }
        }
    }
}
