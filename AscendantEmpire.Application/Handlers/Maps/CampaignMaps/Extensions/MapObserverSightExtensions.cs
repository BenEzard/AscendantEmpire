using static AscendantEmpire.Application.Handlers.Maps.CampaignMaps.GetPlayerCampaignMapHandler;

namespace AscendantEmpire.Application.Handlers.Maps.CampaignMaps.Extensions
{
    internal static class MapObserverSightExtensions
    {
        /// <summary>
        /// Returns all DbIds from OccupiedCoord and all descendant Children across all sights.
        /// Distinct by DbId (no duplicates).
        /// </summary>
        public static List<int> GetAllDbIds(this IEnumerable<MapObserverSight> mapSight)
        {
            ArgumentNullException.ThrowIfNull(mapSight);

            var ids = new HashSet<int>();

            foreach (var sight in mapSight)
            {
                if (sight is null)
                {
                    continue;
                }

                // Parent / occupied tile
                if (sight.OccupiedCoord is not null)
                {
                    ids.Add(sight.OccupiedCoord.DbId);
                }

                // “Children” / visible tiles
                if (sight.SeenTiles is null)
                {
                    continue;
                }

                foreach (var tile in sight.SeenTiles)
                {
                    if (tile is null)
                    {
                        continue;
                    }

                    ids.Add(tile.Id);
                }
            }

            return [.. ids.Distinct()];
        }

        /// <summary>
        /// Reference equality comparer (compares object references, not values).
        /// Helpful to detect cycles in graphs/trees.
        /// </summary>
        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public static readonly ReferenceEqualityComparer<T> Instance = new();

            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}