using MMORPGServer.Common.Enums;
using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Entities;
using Serilog;
using System.Collections.Concurrent;
using System.Text;

namespace MMORPGServer.Database.Ini
{
    public class MapRepository
    {
        private readonly ConcurrentDictionary<short, Map> _maps;
        private readonly MapVisualizer _mapVisualizer;
        private readonly string _basePath;
        private bool _isInitialized;

        public MapRepository()
        {
            _maps = new ConcurrentDictionary<short, Map>();
            _mapVisualizer = new MapVisualizer();
            _basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Database");
            _isInitialized = false;
        }



        public async Task<Map?> GetMapAsync(short mapId)
        {
            if (!_isInitialized)
            {
                Log.Warning("Maps not initialized, call InitializeMapsAsync() first");
                return null;
            }

            _maps.TryGetValue(mapId, out Map? map);
            return map;
        }

        public async Task<IEnumerable<Map>> GetAllMapsAsync()
        {
            if (!_isInitialized)
            {
                Log.Warning("Maps not initialized, returning empty collection");
                return Enumerable.Empty<Map>();
            }

            return _maps.Values;
        }

        public async Task<bool> SaveMapAsync(Map map)
        {
            if (map == null)
            {
                Log.Warning("Cannot save null map");
                return false;
            }

            var success = _maps.TryAdd(map.Id, map);
            if (success)
            {
                Log.Debug("Map {MapId} saved successfully", map.Id);
            }
            else
            {
                Log.Warning("Map {MapId} already exists, cannot save", map.Id);
            }

            return success;
        }

        public async Task<bool> DeleteMapAsync(short mapId)
        {
            var success = _maps.TryRemove(mapId, out _);
            if (success)
            {
                Log.Information("Map {MapId} deleted successfully", mapId);
            }
            else
            {
                Log.Warning("Map {MapId} not found for deletion", mapId);
            }

            return success;
        }

        public async Task InitializeMapsAsync()
        {
            if (_isInitialized)
            {
                Log.Information("Maps already initialized, skipping");
                return;
            }

            Log.Information("Initializing maps system...");

            try
            {
                string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string gameMapPath = Path.Combine(applicationDataPath, @"Database\ini\GameMap.dat");

                if (!File.Exists(gameMapPath))
                {
                    Log.Error("GameMap.dat not found at: {GameMapPath}", gameMapPath);
                    return;
                }

                // Ensure maps directory exists for visualization
                string mapsDir = Path.Combine(AppContext.BaseDirectory, "maps");
                if (!Directory.Exists(mapsDir))
                {
                    Directory.CreateDirectory(mapsDir);
                    Log.Information("Created maps directory: {MapsDir}", mapsDir);
                }

                using var reader = new BinaryReader(File.OpenRead(gameMapPath));
                int mapCount = reader.ReadInt32();
                int loadedMaps = 0;

                Log.Information("Found {MapCount} maps to load", mapCount);

                for (int i = 0; i < mapCount; i++)
                {
                    try
                    {
                        int mapId = reader.ReadInt32();
                        int fileLength = reader.ReadInt32();
                        string fileName = Encoding.ASCII.GetString(reader.ReadBytes(fileLength)).Replace(".7z", ".dmap");
                        _ = reader.ReadInt32(); // puzzleSize

                        var map = await LoadMapDataAsync((short)mapId, fileName);
                        if (map != null)
                        {
                            await SaveMapAsync(map);
                            loadedMaps++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error loading map at index {Index}", i);
                    }
                }

                _isInitialized = true;
                Log.Information("Map initialization completed. Loaded {LoadedMaps}/{TotalMaps} maps successfully",
                    loadedMaps, mapCount);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error during map initialization");
                throw;
            }
        }

        private async Task<Map?> LoadMapDataAsync(short mapId, string fileName)
        {
            try
            {
                string fullPath = Path.Combine(_basePath, fileName);
                if (!File.Exists(fullPath))
                {
                    Log.Error("Map file not found: {FilePath}", fullPath);
                    return null;
                }

                using var reader = new BinaryReader(File.OpenRead(fullPath));
                byte[] skip = reader.ReadBytes(268); // Skip header
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();

                Log.Debug("Loading map {MapId} with dimensions {Width}x{Height}", mapId, width, height);

                var map = new Map(mapId, width, height);

                // Load cell data
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        CellType cellFlag = (CellType)reader.ReadInt16();
                        if (cellFlag == CellType.None)
                        {
                            cellFlag = CellType.Open;
                        }

                        short floorType = reader.ReadInt16();
                        short cellHeight = reader.ReadInt16();

                        if (!IsValidCellType((int)cellFlag))
                        {
                            Log.Warning("Invalid cell type flags: {CellFlag} at {X},{Y} in map {MapId}",
                                cellFlag, x, y, mapId);
                            cellFlag = CellType.Open; // Default to open for invalid cells
                        }

                        map[x, y] = new Cell(cellFlag, cellHeight, floorType);
                    }
                    reader.ReadInt32(); // Skip padding
                }

                // Generate map visualization
                try
                {
                    string imagePath = Path.Combine(AppContext.BaseDirectory, "maps", $"{map.Id}.png");
                    _mapVisualizer.GenerateMapImage(map, imagePath);
                    Log.Debug("Generated map visualization: {ImagePath}", imagePath);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to generate map visualization for map {MapId}", mapId);
                    // Don't fail map loading if visualization fails
                }

                Log.Debug("Successfully loaded map {MapId} ({FileName})", map.Id, fileName);
                return map;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading map data for map {MapId} from file {FileName}", mapId, fileName);
                return null;
            }
        }

        private static bool IsValidCellType(int value)
        {
            const int AllValidFlags = (int)(CellType.Open | CellType.Blocked |
                                           CellType.Gate | CellType.Entity |
                                           CellType.StaticObj | CellType.BlockedObj);

            return value >= 0 && (value & AllValidFlags) == value;
        }

        // Reset method for testing or reinitialization
        public void Reset()
        {
            _maps.Clear();
            _isInitialized = false;
            Log.Information("Map repository reset");
        }

        // Get map count
        public int GetMapCount()
        {
            return _maps.Count;
        }

        // Check if a specific map exists
        public bool MapExists(short mapId)
        {
            return _maps.ContainsKey(mapId);
        }
    }
}
