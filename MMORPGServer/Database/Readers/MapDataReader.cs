using Microsoft.EntityFrameworkCore;
using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Database.Models;
using Serilog;

namespace MMORPGServer.Database.Readers
{
    public class MapDataReader
    {
        private readonly GameDbContext _context;
        private Dictionary<int, MapData> MapsData { get; set; }
        private Dictionary<int, List<MapData>> MapsByGroup { get; set; }
        private Dictionary<int, List<MapData>> MapsByOwner { get; set; }

        public MapDataReader(GameDbContext context)
        {
            _context = context;
            MapsData = new Dictionary<int, MapData>(2000);
            MapsByGroup = new Dictionary<int, List<MapData>>();
            MapsByOwner = new Dictionary<int, List<MapData>>();
        }

        // Indexer to get map by ID
        public MapData this[int mapId]
        {
            get
            {
                if (MapsData.TryGetValue(mapId, out var mapData))
                {
                    return mapData;
                }
                return null;
            }
        }

        // Get maps by group
        public IEnumerable<MapData> GetMapsByGroup(int mapGroup)
        {
            if (MapsByGroup.TryGetValue(mapGroup, out var maps))
            {
                return maps;
            }
            return Enumerable.Empty<MapData>();
        }

        // Get maps by owner
        public IEnumerable<MapData> GetMapsByOwner(int ownerId)
        {
            if (MapsByOwner.TryGetValue(ownerId, out var maps))
            {
                return maps;
            }
            return Enumerable.Empty<MapData>();
        }

        // Get all maps
        public IEnumerable<MapData> GetAllMaps()
        {
            return MapsData.Values;
        }

        // Check if map exists
        public bool MapExists(int mapId)
        {
            return MapsData.ContainsKey(mapId);
        }

        // Get map portal destination
        public int? GetPortalDestination(int mapId)
        {
            var map = this[mapId];
            return map?.LinkMap > 0 ? map.LinkMap : null;
        }

        // Get portal position for map
        public Position? GetPortalPosition(int mapId)
        {
            var map = this[mapId];
            if (map != null && map.Portal0X > 0 && map.Portal0Y > 0)
            {
                return new Position((short)map.Portal0X, (short)map.Portal0Y);
            }
            return null;
        }

        // Get reborn map for a map
        public int? GetRebornMap(int mapId)
        {
            var map = this[mapId];
            return map?.RebornMap > 0 ? map.RebornMap : null;
        }

        // Check if map requires specific level
        public bool CanEnterMap(int mapId, int playerLevel)
        {
            var map = this[mapId];
            if (map == null) return false;

            return map.ResLev == 0 || playerLevel >= map.ResLev;
        }

        // Get maps by server
        public IEnumerable<MapData> GetMapsByServer(int serverId)
        {
            return MapsData.Values.Where(m => m.IdxServer == serverId);
        }

        // Load all maps from database
        public async Task LoadAllMapsAsync()
        {
            // Clear existing data
            MapsData.Clear();
            MapsByGroup.Clear();
            MapsByOwner.Clear();

            // Load all maps from database
            var maps = await _context.MapData
                .AsNoTracking()
                .Where(m => m.DelFlag == 0) // Only load active maps
                .ToListAsync();

            Log.Information("Loaded {Count} map records from database", maps.Count);

            foreach (var map in maps)
            {
                // Add to main dictionary
                MapsData[map.Id] = map;

                // Group by mapgroup
                if (!MapsByGroup.ContainsKey(map.MapGroup))
                {
                    MapsByGroup[map.MapGroup] = new List<MapData>();
                }
                MapsByGroup[map.MapGroup].Add(map);

                // Group by owner
                if (map.OwnerId > 0)
                {
                    if (!MapsByOwner.ContainsKey(map.OwnerId))
                    {
                        MapsByOwner[map.OwnerId] = new List<MapData>();
                    }
                    MapsByOwner[map.OwnerId].Add(map);
                }

                Log.Debug("Added Map ID: {MapId}, Name: {Name}, Group: {Group}, Type: {Type}, Portal: ({X},{Y})",
                    map.Id, map.Name, map.MapGroup, map.Type, map.Portal0X, map.Portal0Y);
            }

            Log.Information("Final Maps loaded: {MapCount} maps, {GroupCount} groups, {OwnerCount} owners",
                MapsData.Count, MapsByGroup.Count, MapsByOwner.Count);
        }

        // Reload specific map
        public async Task ReloadMapAsync(int mapId)
        {
            var map = await _context.MapData
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == mapId && m.DelFlag == 0);

            if (map != null)
            {
                // Remove from old groups if exists
                var oldMap = MapsData.GetValueOrDefault(mapId);
                if (oldMap != null)
                {
                    RemoveFromGroups(oldMap);
                }

                // Add updated map
                MapsData[mapId] = map;
                AddToGroups(map);

                Log.Information("Reloaded map {MapId}: {Name}", mapId, map.Name);
            }
            else
            {
                // Map was deleted or deactivated
                if (MapsData.TryGetValue(mapId, out var removedMap))
                {
                    MapsData.Remove(mapId);
                    RemoveFromGroups(removedMap);
                    Log.Information("Removed map {MapId} from cache", mapId);
                }
            }
        }

        private void AddToGroups(MapData map)
        {
            // Add to group
            if (!MapsByGroup.ContainsKey(map.MapGroup))
            {
                MapsByGroup[map.MapGroup] = new List<MapData>();
            }
            MapsByGroup[map.MapGroup].Add(map);

            // Add to owner
            if (map.OwnerId > 0)
            {
                if (!MapsByOwner.ContainsKey(map.OwnerId))
                {
                    MapsByOwner[map.OwnerId] = new List<MapData>();
                }
                MapsByOwner[map.OwnerId].Add(map);
            }
        }

        private void RemoveFromGroups(MapData map)
        {
            // Remove from group
            if (MapsByGroup.TryGetValue(map.MapGroup, out var groupMaps))
            {
                groupMaps.RemoveAll(m => m.Id == map.Id);
                if (groupMaps.Count == 0)
                {
                    MapsByGroup.Remove(map.MapGroup);
                }
            }

            // Remove from owner
            if (map.OwnerId > 0 && MapsByOwner.TryGetValue(map.OwnerId, out var ownerMaps))
            {
                ownerMaps.RemoveAll(m => m.Id == map.Id);
                if (ownerMaps.Count == 0)
                {
                    MapsByOwner.Remove(map.OwnerId);
                }
            }
        }

        // Debug method to print all maps
        public void LogAllMaps()
        {
            Log.Information("=== All Map Configurations ===");

            foreach (var mapKvp in MapsData.OrderBy(x => x.Key))
            {
                var map = mapKvp.Value;
                Log.Information("Map {MapId}: {Name} ({Description})",
                    map.Id, map.Name, map.DescribeText);
                Log.Information("  Group: {Group}, Owner: {Owner}, Type: {Type}, Server: {Server}",
                    map.MapGroup, map.OwnerId, map.Type, map.IdxServer);
                Log.Information("  Portal: ({X},{Y}) -> Map {LinkMap}, Reborn: Map {RebornMap}",
                    map.Portal0X, map.Portal0Y, map.LinkMap, map.RebornMap);
                Log.Information("  Settings: Weather={Weather}, Music={Music}, ResLev={ResLev}, Color={Color:X}",
                    map.Weather, map.BgMusic, map.ResLev, map.Color);
            }

            Log.Information("=== Maps by Group ===");
            foreach (var groupKvp in MapsByGroup.OrderBy(x => x.Key))
            {
                Log.Information("Group {GroupId}: {Count} maps",
                    groupKvp.Key, groupKvp.Value.Count);
                foreach (var map in groupKvp.Value.OrderBy(m => m.Id))
                {
                    Log.Information("  - {MapId}: {Name}", map.Id, map.Name);
                }
            }

            Log.Information("=== Maps by Owner ===");
            foreach (var ownerKvp in MapsByOwner.OrderBy(x => x.Key))
            {
                Log.Information("Owner {OwnerId}: {Count} maps",
                    ownerKvp.Key, ownerKvp.Value.Count);
                foreach (var map in ownerKvp.Value.OrderBy(m => m.Id))
                {
                    Log.Information("  - {MapId}: {Name}", map.Id, map.Name);
                }
            }
        }

    }

}