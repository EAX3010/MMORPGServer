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
                Log.Warning("DMaps not initialized, call InitializeDMapsAsync() first");
                return null;
            }

            _DMaps.TryGetValue(DMapId, out DMap? DMap);
            return DMap;
        }

        public async Task<IEnumerable<DMap>> GetAllDMapsAsync()
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
                Log.Warning("Cannot save null DMap");
                return false;
            }

            var success = _DMaps.TryAdd(DMap.Id, DMap);
            if (success)
            {
                Log.Debug("DMap {DMapId} saved successfully", DMap.Id);
            }
            else
            {
                Log.Warning("DMap {DMapId} already exists, cannot save", DMap.Id);
            }

            return success;
        }

        public async Task<bool> DeleteDMapAsync(short DMapId)
        {
            var success = _DMaps.TryRemove(DMapId, out _);
            if (success)
            {
                Log.Information("DMap {DMapId} deleted successfully", DMapId);
            }
            else
            {
                Log.Warning("DMap {DMapId} not found for deletion", DMapId);
            }

            return success;
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
                    Log.Error("GameDMap.dat not found at: {GameDMapPath}", gameDMapPath);
                    return;
                }

                // Ensure DMaps directory exists for visualization
                string DMapsDir = Path.Combine(AppContext.BaseDirectory, "Maps");
                if (!Directory.Exists(DMapsDir))
                {
                    Directory.CreateDirectory(DMapsDir);
                    Log.Information("Created DMaps directory: {DMapsDir}", DMapsDir);
                }

                using var reader = new BinaryReader(File.OpenRead(gameDMapPath));
                int DMapCount = reader.ReadInt32();
                int loadeDMaps = 0;

                Log.Information("Found {DMapCount} DMaps to load", DMapCount);

                for (int i = 0; i < DMapCount; i++)
                {
                    try
                    {
                        int DMapId = reader.ReadInt32();
                        int fileLength = reader.ReadInt32();
                        string fileName = Encoding.ASCII.GetString(reader.ReadBytes(fileLength));
                        _ = reader.ReadInt32(); // puzzleSize

                        var DMap = await LoaDMapDataAsync((short)DMapId, fileName);
                        if (DMap != null)
                        {
                            await SaveDMapAsync(DMap);
                            loadeDMaps++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error loading DMap at index {Index}", i);
                    }
                }

                _isInitialized = true;
                Log.Information("DMap initialization completed. Loaded {LoadeDMaps}/{TotalDMaps} DMaps successfully",
                    loadeDMaps, DMapCount);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error during DMap initialization");
                throw;
            }
        }
        private async Task<DMap?> LoaDMapDataAsync(short DMapId, string fileName)
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
                    SevenZipArchiveEntry entry = archive.Entries.FirstOrDefault();
                    if (entry.IsDirectory)
                    {
                        Log.Error("DMap file {FileName} is a directory, not a valid DMap file", fileName);
                        return null;
                    }
                    if (!entry.Name.EndsWith(".dmap", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Warning("Skipping non-DMap entry: {EntryName}", entry.Name);
                        return null;
                    }

                    using var memoryStream = new MemoryStream(new byte[entry.UncompressedSize], 0, (int)entry.UncompressedSize);
                    entry.Extract(memoryStream);
                    memoryStream.Position = 0;
                    using var reader = new BinaryReader(memoryStream);

                    long someId = reader.ReadInt64();
                    string pulFile = Encoding.ASCII.GetString(reader.ReadBytes(256));
                    int junk = reader.ReadInt32();

                    int width = reader.ReadInt32();
                    int height = reader.ReadInt32();

                    Log.Debug("Loading DMap {DMapId} with dimensions {Width}x{Height}", DMapId, width, height);

                    var DMap = new DMap(DMapId, width, height);
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
                                Log.Warning("Invalid cell type flags: {CellFlag} at {X},{Y} in DMap {DMapId}",
                                    cellFlag, x, y, DMapId);
                                cellFlag = CellType.Open; // Default to open for invalid cells
                            }

                            DMap[x, y] = new Cell(cellFlag, cellHeight, floorType);
                        }
                        _ = reader.ReadInt32(); // Skip padding
                    }
                    // Generate DMap visualization
                    try
                    {
                        string imagePath = Path.Combine(AppContext.BaseDirectory, "DMaps", $"{DMap.Id}.png");
                        _DMapVisualizer.GenerateMapImage(DMap, imagePath);
                        Log.Debug("Generated DMap visualization: {ImagePath}", imagePath);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to generate DMap visualization for DMap {DMapId}", DMapId);
                    }

                    Log.Debug("Successfully loaded DMap {DMapId} ({FileName})", DMap.Id, fileName);
                    return DMap;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading DMap data for DMap {DMapId} from file {FileName}", DMapId, fileName);
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
            _DMaps.Clear();
            _isInitialized = false;
            Log.Information("DMap repository reset");
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
