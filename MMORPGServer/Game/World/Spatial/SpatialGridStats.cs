namespace MMORPGServer.Game.World.Spatial
{
    /// <summary>
    /// Performance statistics for the spatial grid
    /// </summary>
    public readonly record struct SpatialGridStats
    {
        public int TotalObjects { get; init; }
        public int ActiveObjects { get; init; }
        public int TotalQueries { get; init; }
        public int ActiveCells { get; init; }
        public int CellSize { get; init; }
        public (int Width, int Height) GridDimensions { get; init; }
        public long MemoryUsage { get; init; }
    }
}
