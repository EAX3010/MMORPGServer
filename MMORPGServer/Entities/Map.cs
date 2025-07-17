using MMORPGServer.Common.Interfaces;
using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Database.Models;
using System.Collections.Concurrent;
namespace MMORPGServer.Entities
{
    public class Map : IDisposable
    {
        private bool _disposed = false;
        private readonly DMap MapData;
        private readonly ConcurrentDictionary<int, Player> _players;
        private readonly Timer _updateTimer;
        private MapData Configuration { get; }

        // Entity collections
        public IReadOnlyDictionary<int, Player> Players => _players;

        // Map state
        public DateTime CreatedTime { get; }
        public DateTime LastActivity { get; private set; }
        public int TotalEntities => _players.Count;
        public bool IsActive => DateTime.UtcNow - LastActivity < TimeSpan.FromMinutes(30);
        public int MapId => Configuration.Id;
        public int MapBaseId => Configuration.MapDoc;
        public Map(DMap mapData, MapData configuration)
        {
            MapData = mapData ?? throw new ArgumentNullException(nameof(mapData));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _players = new ConcurrentDictionary<int, Player>();

            CreatedTime = DateTime.UtcNow;
            LastActivity = DateTime.UtcNow;

            // Start update timer (60 FPS = ~16ms)
            _updateTimer = new Timer(Update, null, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        }

        #region Player Management

        /// <summary>
        /// Adds a player to the map
        /// </summary>
        public async ValueTask<bool> AddPlayerAsync(Player player)
        {
            if (player == null) return false;

            // Add to map data first
            if (!MapData.AddEntity(player))
                return false;

            // Add to players collection
            if (_players.TryAdd(player.Id, player))
            {
                player.Map = this;
                LastActivity = DateTime.UtcNow;
                return true;
            }
            else
            {
                MapData.RemoveEntity(player);
                return false;
            }
        }

        /// <summary>
        /// Removes a player from the map
        /// </summary>
        public async ValueTask<bool> RemovePlayerAsync(int playerId)
        {
            if (_players.TryRemove(playerId, out Player player))
            {
                MapData.RemoveEntity(playerId);
                LastActivity = DateTime.UtcNow;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Moves a player to a new position
        /// </summary>
        public async ValueTask<bool> MovePlayerAsync(int playerId, Position newPosition)
        {
            if (!_players.TryGetValue(playerId, out Player player))
                return false;

            if (MapData.TryMoveEntity(player, newPosition))
            {
                LastActivity = DateTime.UtcNow;
                return true;
            }
            return false;
        }

        #endregion
        #region Map Queries

        /// <summary>
        /// Gets all entities in range of a position
        /// </summary>
        public IEnumerable<MapObject> GetEntitiesInRange(Position position, float range)
        {
            return MapData.GetEntitiesInRange(position, range);
        }

        /// <summary>
        /// Gets all players in range of a position
        /// </summary>
        public IEnumerable<Player> GetPlayersInRange(Position position, float range)
        {
            return GetEntitiesInRange(position, range).OfType<Player>();
        }


        /// <summary>
        /// Checks if position is valid for placement
        /// </summary>
        public bool IsValidPosition(Position position)
        {
            return MapData.IsValidPosition(position);
        }

        /// <summary>
        /// Gets a valid spawn point
        /// </summary>
        public Position SpawnPoint() => new Position((short)this.Configuration.Portal0X, (short)Configuration.Portal0Y);

        #endregion

        #region Portal Handling

        /// <summary>
        /// Handles player portal usage
        /// </summary>
        public async Task<int?> HandlePortalUsage(Player player)
        {
            var destinationMapId = MapData.GetPortalDestination(player.Position);
            if (destinationMapId.HasValue)
            {
                LastActivity = DateTime.UtcNow;
                return destinationMapId;
            }
            return null;
        }

        #endregion

        #region Update Loop
        private void Update(object state)
        {
            if (_disposed) return;

            try
            {
                var deltaTime = 0.016f; // ~60 FPS


            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in map update loop: {ex.Message}");
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Clears all entities from the map
        /// </summary>
        public async Task ClearAllEntitiesAsync()
        {
            // Remove all players
            var playerIds = _players.Keys.ToList();
            foreach (var playerId in playerIds)
            {
                await RemovePlayerAsync(playerId);
            }
            MapData.Clear();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _updateTimer?.Dispose();

                // Clear all entities
                Task.Run(async () => await ClearAllEntitiesAsync()).Wait();

                // Dispose map data
                MapData?.Dispose();
            }

            _disposed = true;
        }

        #endregion
    }
}
