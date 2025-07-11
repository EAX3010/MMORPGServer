using MMORPGServer.Database;
using MMORPGServer.Entities;
using Serilog;
using System.Collections.Concurrent;

namespace MMORPGServer.Services
{
    internal class MapManager
    {
        private readonly ConcurrentDictionary<int, Map> Maps = new();

        public MapManager()
        {
            Log.Information("Starting MapManager initialization...");
            LoadAllMaps();
            Log.Information("MapManager loaded {MapCount} maps", Maps.Count);
        }

        /// <summary>
        /// Loads all maps from database
        /// </summary>
        private void LoadAllMaps()
        {
            var allMapData = RepositoryManager.MapDataReader.GetAllMaps();
            int loadedCount = 0;
            int failedCount = 0;

            foreach (var mapData in allMapData)
            {
                try
                {
                    var dMap = RepositoryManager.DMapReader.GetDMapAsync((short)mapData.MapDoc).GetAwaiter().GetResult();

                    if (dMap != null)
                    {
                        Map map = new Map(dMap, mapData);
                        Maps.TryAdd(mapData.Id, map);
                        loadedCount++;

                        Log.Debug("Loaded map {MapId}: {MapName}", mapData.Id, mapData.Name);
                    }
                    else
                    {
                        Log.Warning("Failed to load DMap for {MapId}: {MapName} (MapDoc: {MapDoc})",
                            mapData.Id, mapData.Name, mapData.MapDoc);
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error loading map {MapId}: {MapName}", mapData.Id, mapData.Name);
                    failedCount++;
                }
            }

            Log.Information("Map loading complete: {LoadedCount} loaded, {FailedCount} failed",
                loadedCount, failedCount);
        }

        /// <summary>
        /// Gets a map by ID
        /// </summary>
        public Map GetMap(int mapId)
        {
            Maps.TryGetValue(mapId, out Map map);
            return map;
        }

        /// <summary>
        /// Indexer for easy access
        /// </summary>
        public Map this[int mapId] => GetMap(mapId);
    }
}