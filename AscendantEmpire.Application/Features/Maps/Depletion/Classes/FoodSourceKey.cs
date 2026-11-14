using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Classes;

/// <summary>
/// Differentiates a tile's food sources from another: identity only, so an amount taken can be keyed by the source it came out of.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="Resource"/> is set for a <see cref="FoodSourceType.Tile"/> source and names the <c>*Taken</c> column the spending adds to;
/// <paramref name="MapResourceInstanceId"/> is set for a <see cref="FoodSourceType.MapResource"/> source and names the instance whose quantity it lowers.
/// Exactly one of the two is set.
/// </para>
/// <para>
/// <paramref name="AssetId"/> is carried on both, because what is eaten has to be nameable as an Asset: a Force stripping surplus food into its bank will
/// bank it by that id. It is not identity enough on its own - two berry patches on one tile yield the same Asset and deplete apart - which is why the
/// instance id is in the key as well.
/// </para>
/// <para>
/// Comparable because <see cref="Services.ProportionalSplitter"/> breaks a tie on the lowest key, so that a split depends only on its inputs and never on
/// the order they arrived in.
/// </para>
/// </remarks>
internal readonly record struct FoodSourceKey(
    FoodSourceType SourceType,
    SustenanceResource? Resource,
    int? MapResourceInstanceId,
    int AssetId) : IComparable<FoodSourceKey>
{
    public int CompareTo(FoodSourceKey other)
    {
        int bySourceType = SourceType.CompareTo(other.SourceType);

        if (bySourceType != 0)
        {
            return bySourceType;
        }

        int byResource = Nullable.Compare(Resource, other.Resource);

        if (byResource != 0)
        {
            return byResource;
        }

        int byInstance = Nullable.Compare(MapResourceInstanceId, other.MapResourceInstanceId);

        return byInstance != 0 ? byInstance : AssetId.CompareTo(other.AssetId);
    }
}
