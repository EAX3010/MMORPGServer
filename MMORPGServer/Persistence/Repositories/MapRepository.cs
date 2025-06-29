using Microsoft.Extensions.Logging;
using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Entities;
using System.Collections.Concurrent;
using System.Text;

namespace MMORPGServer.Persistence.Repositories
{
    public class MapRepository : IMapRepository
    {
        private readonly ILogger<MapRepository> _logger;
        private readonly ConcurrentDictionary<short, Map> _maps;
        private readonly MapVisualizer _mapVisualizer;
        private readonly string _basePath;

        public MapRepository(ILogger<MapRepository> logger, MapVisualizer mapVisualizer)
        {
            _logger = logger;
            _mapVisualizer = mapVisualizer;
            _maps = new ConcurrentDictionary<short, Map>();
            _basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Database");

        }

        public async Task<Map> GetMapAsync(short mapId)
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

        public async Task<bool> DeleteMapAsync(short mapId)
        {
            return _maps.TryRemove(mapId, out _);
        }


        public async Task InitializeMapsAsync()
        {
            string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string gameMapPath = Path.Combine(applicationDataPath, @"Database\ini\GameMap.dat");
            _logger.LogInformation("Initializing maps system ...");

            if (!File.Exists(gameMapPath))
            {
                _logger.LogError("{0} Not found", gameMapPath);
                return;
            }

            using BinaryReader reader = new(File.OpenRead(gameMapPath));
            int mapCount = reader.ReadInt32();
            int i = 0;
            for (i = 0; i < mapCount; i++)
            {
                int mapId = reader.ReadInt32();
                int fileLength = reader.ReadInt32();
                string fileName = Encoding.ASCII.GetString(reader.ReadBytes(fileLength)).Replace(".7z", ".dmap");
                _ = reader.ReadInt32();//  puzzleSize

                var map = await LoadMapDataAsync((short)mapId, fileName);

                await SaveMapAsync(map);


            }
            _logger.LogInformation("Map initialization completed with total maps of {i}", i);
            async Task<Map> LoadMapDataAsync(short mapId, string fileName)
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
                    static bool IsValidCellType(int value)
                    {
                        const int AllValidFlags = (int)(CellType.Open | CellType.Blocked |
                                                       CellType.Gate | CellType.Entity |
                                                       CellType.StaticObj | CellType.BlockedObj);

                        return value >= 0 && (value & AllValidFlags) == value;
                    }

                    // Usage:

                    // Load cell data
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            CellType CellFlag = (CellType)reader.ReadInt16();
                            if (CellFlag == CellType.None)
                            {
                                CellFlag = CellType.Open;
                            }
                            short FloorType = reader.ReadInt16();
                            if (!IsValidCellType((int)CellFlag))
                            {
                                _logger.LogError("Invalid cell type flags: {CellFlag}", CellFlag);
                            }
                            short cellHeight = reader.ReadInt16();
                            map[x, y] = new Cell(CellFlag, cellHeight, FloorType);

                        }
                        reader.ReadInt32(); // Skip padding
                    }

                    // Load portals
                    //int totalPortals = reader.ReadInt32();
                    //for (int i = 0; i < totalPortals; i++)
                    //{
                    //    int portalX = reader.ReadInt32();
                    //    int portalY = reader.ReadInt32();
                    //    int destinationId = reader.ReadInt32();

                    //    map.AddPortal(destinationId, new Position((short)portalX, (short)portalY));

                    //    //// Mark portal cells
                    //    //for (int x = 0; x < 3; x++)
                    //    //{
                    //    //    for (int y = 0; y < 3; y++)
                    //    //    {
                    //    //        if (portalY + y < height && portalX + x < width)
                    //    //        {
                    //    //            map[portalX + x, portalY + y] = map[portalX + x, portalY + y]
                    //    //                .SetArgument((short)destinationId);
                    //    //        }
                    //    //    }
                    //    //}
                    //}
                    string imagePath = Path.Combine(AppContext.BaseDirectory, "maps", $"{map.Id}.png");
                    _mapVisualizer.GenerateMapImage(map, imagePath);
                    _logger.LogDebug("Successfully loaded map {MapId} ({MapName})", map.Id, fileName);
                    return map;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading map data for map {MapId}", mapId);
                    return null;
                }
            }
        }
    }
}