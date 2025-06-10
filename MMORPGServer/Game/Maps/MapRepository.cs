namespace MMORPGServer.Game.Maps
{
    public class MapRepository : IMapRepository
    {
        private readonly ILogger<MapRepository> _logger;
        private readonly ConcurrentDictionary<ushort, Map> _maps;
        private readonly string _basePath;

        public MapRepository(ILogger<MapRepository> logger)
        {
            _logger = logger;
            _maps = new ConcurrentDictionary<ushort, Map>();
            _basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Database");
        }

        public async Task<Map?> GetMapAsync(ushort mapId)
        {
            _maps.TryGetValue(mapId, out var map);
            return map;
        }

        public async Task<IEnumerable<Map>> GetAllMapsAsync()
        {
            return _maps.Values;
        }

        public async Task<bool> SaveMapAsync(Map map)
        {
            return _maps.TryAdd(map.Id, map);
        }

        public async Task<bool> DeleteMapAsync(ushort mapId)
        {
            return _maps.TryRemove(mapId, out _);
        }

        public async Task<Map> CreateMapAsync(ushort mapId, string name, int width, int height)
        {
            var map = new Map(mapId, width, height);
            if (!_maps.TryAdd(mapId, map))
            {
                throw new InvalidOperationException($"Map with ID {mapId} already exists");
            }
            return map;
        }

        public async Task<Map?> LoadMapDataAsync(ushort mapId, string fileName)
        {
            try
            {
                var fullPath = Path.Combine(_basePath, fileName);
                if (!File.Exists(fullPath))
                {
                    _logger.LogError("Map file not found: {FilePath}", fullPath);
                    return null;
                }

                using var reader = new BinaryReader(File.OpenRead(fullPath));
                var skip = reader.ReadBytes(268); // Skip header
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();

                var map = new Map(mapId, width, height);

                // Load cell data
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        CellType baseType = reader.ReadUInt16() == 0 ? CellType.Open : CellType.Terrain;
                        ushort cellFlag = reader.ReadUInt16();
                        ushort cellHeight = reader.ReadUInt16();
                        map[x, y] = new Cell(baseType, cellHeight, cellFlag);
                    }
                    reader.ReadUInt32(); // Skip padding
                }

                // Load portals
                int totalPortals = reader.ReadInt32();
                for (int i = 0; i < totalPortals; i++)
                {
                    int portalX = reader.ReadInt32() - 1;
                    int portalY = reader.ReadInt32() - 1;
                    int destinationId = reader.ReadInt32();

                    map.AddPortal(destinationId, new Position((short)portalX, (short)portalY));

                    // Mark portal cells
                    for (int x = 0; x < 3; x++)
                    {
                        for (int y = 0; y < 3; y++)
                        {
                            if (portalY + y < height && portalX + x < width)
                            {
                                map[portalX + x, portalY + y]
                                    .AddFlag(CellType.Portal)
                                    .SetArgument((ushort)destinationId);
                            }
                        }
                    }
                }

                _logger.LogInformation("Successfully loaded map {MapId} ({MapName})", map.Id);
                _maps.TryAdd(mapId, map);
                return map;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading map data for map {MapId}", mapId);
                return null;
            }
        }

        public async Task<Position?> GetValidSpawnPointAsync(Map map)
        {
            var random = new Random();
            var attempts = 0;
            const int maxAttempts = 100;

            while (attempts < maxAttempts)
            {
                var x = random.Next(0, map.Width);
                var y = random.Next(0, map.Height);

                var cell = map[x, y];
                if (cell[CellType.Open] && !cell[CellType.Portal])
                {
                    return new Position((short)x, (short)y);
                }

                attempts++;
            }

            _logger.LogWarning("Could not find valid spawn point for map {MapId} after {Attempts} attempts",
                map.Id, maxAttempts);
            return null;
        }
    }
}