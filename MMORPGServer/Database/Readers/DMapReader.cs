using Aspose.Zip.SevenZip;
using MMORPGServer.Common.Enums;
using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Entities;
using Serilog;
using System.Collections.Concurrent;
using System.Text;

namespace MMORPGServer.Database.Readers
{
    public class DMapReader
    {
        private readonly ConcurrentDictionary<short, DMap> _DMaps;
        private readonly DMapVisualizer _DMapVisualizer;
        private readonly string _basePath;
        private bool _isInitialized;

        public DMapReader()
        {
            _DMaps = new ConcurrentDictionary<short, DMap>();
            _DMapVisualizer = new DMapVisualizer();
            _basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Database");
            _isInitialized = false;
        }

        public async Task<DMap?> GetDMapAsync(short DMapId)
        {
            if (!_isInitialized)
            {
                Log.Warning("Attempted to get DMap {DMapId} before initialization", DMapId);
                return null;
            }

            if (_DMaps.TryGetValue(DMapId, out DMap? DMap))
            {
                return DMap;
            }

            Log.Warning("DMap with ID {DMapId} not found in cache", DMapId);
            return null;
        }

        public IEnumerable<DMap> GetAllDMaps()
        {
            if (!_isInitialized)
            {
                Log.Warning("DMaps not initialized, returning empty collection");
                return Enumerable.Empty<DMap>();
            }

            return _DMaps.Values;
        }

        public async Task<bool> SaveDMapAsync(DMap DMap)
        {
            if (DMap == null)
            {
                Log.Warning("Cannot save a null DMap object");
                return false;
            }

            if (_DMaps.TryAdd(DMap.Id, DMap))
            {
                Log.Debug("DMap {DMapId} cached successfully", DMap.Id);
                return true;
            }
            else
            {
                Log.Warning("DMap {DMapId} already exists in cache, cannot save", DMap.Id);
                return false;
            }
        }

        public async Task<bool> DeleteDMapAsync(short DMapId)
        {
            if (_DMaps.TryRemove(DMapId, out _))
            {
                Log.Information("DMap {DMapId} removed from cache", DMapId);
                return true;
            }
            else
            {
                Log.Warning("DMap {DMapId} not found for deletion", DMapId);
                return false;
            }
        }

        public async Task InitializeDMapsAsync()
        {
            if (_isInitialized)
            {
                Log.Information("DMaps already initialized, skipping");
                return;
            }

            Log.Information("Initializing DMaps system...");

            try
            {
                string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string gameDMapPath = Path.Combine(applicationDataPath, @"Database\ini\GameMap.dat");

                if (!File.Exists(gameDMapPath))
                {
                    Log.Error("GameMap.dat not found at: {GameDMapPath}", gameDMapPath);
                    return;
                }

                // Ensure DMaps directory exists for visualization
                string dMapsDir = Path.Combine(AppContext.BaseDirectory, "DMaps");
                if (!Directory.Exists(dMapsDir))
                {
                    Directory.CreateDirectory(dMapsDir);
                    Log.Debug("Created DMaps directory: {DMapsDir}", dMapsDir);
                }

                using var reader = new BinaryReader(File.OpenRead(gameDMapPath));
                int dMapCount = reader.ReadInt32();
                int loadedDMaps = 0;

                Log.Information("Found {DMapCount} DMaps to load from GameMap.dat", dMapCount);

                for (int i = 0; i < dMapCount; i++)
                {
                    try
                    {
                        int dMapId = reader.ReadInt32();
                        int fileLength = reader.ReadInt32();
                        string fileName = Encoding.ASCII.GetString(reader.ReadBytes(fileLength));
                        _ = reader.ReadInt32(); // puzzleSize

                        var dMap = await LoadDMapDataAsync((short)dMapId, fileName);
                        if (dMap != null)
                        {
                            await SaveDMapAsync(dMap);
                            loadedDMaps++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error loading DMap at index {Index}", i);
                    }
                }

                _isInitialized = true;
                Log.Information("DMap initialization completed. Loaded {LoadedDMaps}/{TotalDMaps} DMaps successfully",
                    loadedDMaps, dMapCount);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error during DMap initialization");
                throw;
            }
        }
        private async Task<DMap?> LoadDMapDataAsync(short dMapId, string fileName)
        {
            try
            {
                string fullPath = Path.Combine(_basePath, fileName);
                if (!File.Exists(fullPath))
                {
                    Log.Error("DMap file not found: {FilePath}", fullPath);
                    return null;
                }

                using (SevenZipArchive archive = new SevenZipArchive(fullPath))
                {
                    SevenZipArchiveEntry? entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".dmap", StringComparison.OrdinalIgnoreCase));
                    if (entry == null || entry.IsDirectory)
                    {
                        Log.Error("No valid .dmap entry found in archive {FileName} for DMapId {DMapId}", fileName, dMapId);
                        return null;
                    }

                    using var memoryStream = new MemoryStream(new byte[entry.UncompressedSize], 0, (int)entry.UncompressedSize);
                    entry.Extract(memoryStream);
                    memoryStream.Position = 0;
                    using var reader = new BinaryReader(memoryStream);

                    _ = reader.ReadInt64(); // someId
                    _ = Encoding.ASCII.GetString(reader.ReadBytes(256)); // pulFile
                    _ = reader.ReadInt32(); // junk

                    int width = reader.ReadInt32();
                    int height = reader.ReadInt32();

                    Log.Debug("Loading DMap {DMapId} ({FileName}) with dimensions {Width}x{Height}", dMapId, fileName, width, height);

                    var dMap = new DMap(dMapId, width, height);
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
                                Log.Warning("Invalid cell type flags: {CellFlag} at ({X},{Y}) in DMap {DMapId}",
                                    cellFlag, x, y, dMapId);
                                cellFlag = CellType.Open; // Default to open for invalid cells
                            }

                            dMap[x, y] = new Cell(cellFlag, cellHeight, floorType);
                        }
                        _ = reader.ReadInt32(); // Skip padding
                    }
                    // Generate DMap visualization
                    try
                    {
                        string imagePath = Path.Combine(AppContext.BaseDirectory, "DMaps", $"{dMap.Id}.png");
                        _DMapVisualizer.GenerateMapImage(dMap, imagePath);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to generate DMap visualization for DMap {DMapId}", dMapId);
                    }

                    Log.Debug("Successfully loaded DMap {DMapId} ({FileName})", dMap.Id, fileName);
                    return dMap;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading DMap data for DMap {DMapId} from file {FileName}", dMapId, fileName);
                return null;
            }
        }

        private static bool IsValidCellType(int value)
        {
            const int AllValidFlags = (int)(CellType.Open | CellType.Blocked |
                                           CellType.Gate | CellType.Entity |
                                           CellType.StaticObj | CellType.BlockedObj);

            return value >= 0 && (value & ~AllValidFlags) == 0;
        }

        // Reset method for testing or reinitialization
        public void Reset()
        {
            _DMaps.Clear();
            _isInitialized = false;
            Log.Information("DMap repository has been reset");
        }

        // Get DMap count
        public int GetDMapCount()
        {
            return _DMaps.Count;
        }

        // Check if a specific DMap exists
        public bool DMapExists(short DMapId)
        {
            return _DMaps.ContainsKey(DMapId);
        }
    }
}
