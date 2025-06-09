namespace MMORPGServer.Game.World.Spatial
{
    /// <summary>
    /// Extension methods for integrating with existing map system
    /// </summary>
    public static class SpatialExtensions
    {
        /// <summary>
        /// Convert your existing MapView to use the new spatial system
        /// </summary>
        public static SpatialHashGrid<MapObject> ToSpatialGrid(this Map map)
        {
            return new SpatialHashGrid<MapObject>(map.Width, map.Height);
        }

        /// <summary>
        /// Query objects in screen area (replaces the old 3x3 block query)
        /// </summary>
        public static IEnumerable<T> QueryScreen<T>(this SpatialHashGrid<T> grid, Vector2 center, int screenRadius = 50)
            where T : class, ISpatialObject
        {
            return grid.QueryRadius(center, screenRadius);
        }
    }
}
