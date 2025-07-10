using Microsoft.EntityFrameworkCore;
using MMORPGServer.Database.Models;
using Serilog;

namespace MMORPGServer.Database.Readers
{
    public class CqPointAllotReader
    {
        private readonly GameDbContext _context;

        // Main lookup: [ClassId][Level] -> Stats
        public Dictionary<int, Dictionary<int, PointAllot>> Stats { get; private set; }

        public CqPointAllotReader(GameDbContext context)
        {
            _context = context;
            Stats = new Dictionary<int, Dictionary<int, PointAllot>>();
        }

        // Indexer to get stats with [classId, level] syntax
        public PointAllot this[int classId, int level]
        {
            get
            {
                if (Stats.TryGetValue(classId, out var classStats))
                {
                    if (classStats.TryGetValue(level, out var pointAllot))
                    {
                        return pointAllot;
                    }
                }
                return null;
            }
        }

        // Load all class/level stats from database
        public async Task LoadAllStatsAsync()
        {
            // Clear existing data
            Stats.Clear();

            // Load all class level stats
            var stats = await _context.PointAllot.ToListAsync();

            Log.Information("Loaded {Count} point allot records from database", stats.Count);

            foreach (var stat in stats)
            {
                // Initialize class dictionary if not exists
                if (!Stats.ContainsKey(stat.ClassId))
                {
                    Stats[stat.ClassId] = new Dictionary<int, PointAllot>();
                    Log.Debug("Created new class dictionary for ClassId: {ClassId}", stat.ClassId);
                }
                Stats[stat.ClassId][stat.Level] = stat;
                Log.Debug("Added ClassId: {ClassId}, Level: {Level}, STR: {Strength}, AGI: {Agility}, VIT: {Vitality}, SPI: {Spirit}",
                    stat.ClassId, stat.Level, stat.Strength, stat.Agility, stat.Vitality, stat.Spirit);
            }

            Log.Information("Final Stats loaded: {ClassCount} classes", Stats.Count);
        }

        // Debug method to print all stats
        public void LogAllStats()
        {
            Log.Information("=== All Point Allot Stats ===");
            foreach (var classKvp in Stats.OrderBy(x => x.Key))
            {
                Log.Information("Class {ClassId}:", classKvp.Key);
                foreach (var levelKvp in classKvp.Value.OrderBy(x => x.Key))
                {
                    var stats = levelKvp.Value;
                    Log.Information("  Level {Level}: STR={Strength}, AGI={Agility}, VIT={Vitality}, SPI={Spirit}",
                        levelKvp.Key, stats.Strength, stats.Agility, stats.Vitality, stats.Spirit);
                }
            }
        }
    }
}