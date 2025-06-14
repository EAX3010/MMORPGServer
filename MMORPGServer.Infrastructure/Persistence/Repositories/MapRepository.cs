using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Entities;
using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.Interfaces;
using MMORPGServer.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace MMORPGServer.Infrastructure.Persistence.Repositories
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

        public async Task<Map> GetMapAsync(ushort mapId)
        {
            _maps.TryGetValue(mapId, out Map map);
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
            Map map = new Map(mapId, width, height);
            if (!_maps.TryAdd(mapId, map))
            {
                throw new InvalidOperationException($"Map with ID {mapId} already exists");
            }
            return map;
        }

        public async Task<Map> LoadMapDataAsync(ushort mapId, string fileName)
        {
            try
            {
                string fullPath = Path.Combine(_basePath, fileName);
                if (!File.Exists(fullPath))
                {
                    _logger.LogError("Map file not found: {FilePath}", fullPath);
                    return null;
                }

                using BinaryReader reader = new BinaryReader(File.OpenRead(fullPath));
                byte[] skip = reader.ReadBytes(268); // Skip header
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();

                Map map = new Map(mapId, width, height);

                // Load cell data
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        CellType baseType = (CellType)reader.ReadUInt16();
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
                    int portalX = reader.ReadInt32();
                    int portalY = reader.ReadInt32();
                    int destinationId = reader.ReadInt32();

                    map.AddPortal(destinationId, new Position((short)portalX, (short)portalY));

                    // Mark portal cells
                    for (int x = 0; x < 3; x++)
                    {
                        for (int y = 0; y < 3; y++)
                        {
                            if (portalY + y < height && portalX + x < width)
                            {
                                map[portalX + x, portalY + y] = map[portalX + x, portalY + y].RemoveFlag(CellType.Blocked).AddFlag(CellType.Open)
                                    .AddFlag(CellType.Portal)
                                    .SetArgument((ushort)destinationId);
                            }
                        }
                    }
                }
                string imagePath = Path.Combine(AppContext.BaseDirectory, "maps", $"{map.Id}.png");
                new MapVisualizer().GenerateMapImage(map, imagePath);
                _logger.LogDebug("Successfully loaded map {MapId} ({MapName})", map.Id, fileName);
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
            Random random = new Random();
            int attempts = 0;
            const int maxAttempts = 100;

            while (attempts < maxAttempts)
            {
                int x = random.Next(0, map.Width);
                int y = random.Next(0, map.Height);

                Cell cell = map[x, y];
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