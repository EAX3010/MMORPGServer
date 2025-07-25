﻿using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Database;
using MMORPGServer.Entities;
using Serilog;
using System.Collections.Concurrent;

namespace MMORPGServer.Services
{
    public class MapManager
    {
        private readonly ConcurrentDictionary<int, Map> Maps = new();
        public MapManager()
        {
            Log.Information("Starting MapManager initialization...");
            LoadAllMapsAsync().GetAwaiter().GetResult();
            Log.Information("MapManager loaded {MapCount} maps", Maps.Count);
        }

        /// <summary>
        /// Loads all maps from the database asynchronously.
        /// </summary>
        private async Task LoadAllMapsAsync()
        {
            var allMapData = RepositoryManager.MapDataReader.GetAllMaps();
            int loadedCount = 0;
            int failedCount = 0;

            foreach (var mapData in allMapData)
            {
                try
                {
                    var dMap = await RepositoryManager.DMapReader.GetDMapAsync((short)mapData.MapDoc);
                    if (dMap != null)
                    {
                        var newDMap = new DMap((short)dMap.Id, dMap.Width, dMap.Height);
                        for (int y = 0; y < newDMap.Height; y++)
                        {
                            for (int x = 0; x < newDMap.Width; x++)
                            {
                                newDMap[x, y] = new Cell(dMap[x, y].Flags, dMap[x, y].Argument, dMap[x, y].FloorType);
                            }
                        }
                        Map map = new Map(newDMap, mapData);
                        _ = Maps.TryAdd(mapData.Id, map);
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
        public async ValueTask<Map?> GetMapAsync(int mapId)
        {
            if (Maps.TryGetValue(mapId, out Map map))
            {
                return map;
            }
            Log.Warning("Attempted to get non-existent map with ID {MapId}", mapId);
            return null;
        }
        /// <summary>
        /// Gets a map by ID
        /// </summary>
        public Map? GetMap(int mapId)
        {
            if (Maps.TryGetValue(mapId, out Map map))
            {
                return map;
            }
            Log.Warning("Attempted to get non-existent map with ID {MapId}", mapId);
            return null;
        }
        /// <summary>
        /// Gets total number of maps loaded
        /// </summary>
        public int GetTotalMaps()
        {
            return Maps.Count;
        }
        /// <summary>
        /// Indexer for easy access
        /// </summary>
        public Map this[int mapId] => GetMap(mapId);

    }
}
