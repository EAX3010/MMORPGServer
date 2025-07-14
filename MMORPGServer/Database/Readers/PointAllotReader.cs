using Microsoft.EntityFrameworkCore;
using MMORPGServer.Common.Enums;
using MMORPGServer.Database.Models;
using Serilog;

namespace MMORPGServer.Database.Readers
{
    public class PointAllotReader
    {
        private readonly GameDbContext _context;

        private Dictionary<int, Dictionary<int, PointAllotData>> Stats { get; set; }

        public PointAllotReader(GameDbContext context)
        {
            _context = context;
            Stats = new Dictionary<int, Dictionary<int, PointAllotData>>(1200);
        }

        // Indexer to get stats with [classId, level] syntax
        public PointAllotData? this[ClassType classId, int level]
        {
            get
            {
                int classIdValue = (int)classId / 1000;
                if (Stats.TryGetValue(classIdValue, out var classStats))
                {
                    if (classStats.TryGetValue(level, out var pointAllot))
                    {
                        return pointAllot;
                    }
                }
                Log.Warning("PointAllot data not found for Class: {Class} (ID: {ClassIdValue}), Level: {Level}", classId, classIdValue, level);
                return null;
            }
        }

        // Load all class/level stats from database
        public async Task LoadAllStatsAsync()
        {
            try
            {
                Log.Information("Loading point allotment stats from database...");
                // Clear existing data
                Stats.Clear();

                // Load all class level stats
                var stats = await _context.PointAllot.AsNoTracking().ToListAsync();

                Log.Information("Loaded {Count} point allot records from database", stats.Count);

                foreach (var stat in stats)
                {
                    // Initialize class dictionary if not exists
                    if (!Stats.ContainsKey(stat.ClassId))
                    {
                        Stats[stat.ClassId] = new Dictionary<int, PointAllotData>();
                        Log.Debug("Created new point allot dictionary for ClassId: {ClassId}", stat.ClassId);
                    }
                    Stats[stat.ClassId][stat.Level] = stat;
                    Log.Debug("Cached PointAllot: ClassId={ClassId}, Level={Level}, STR={Strength}, AGI={Agility}, VIT={Vitality}, SPI={Spirit}",
                        stat.ClassId, stat.Level, stat.Strength, stat.Agility, stat.Vitality, stat.Spirit);

                    // Special handling for base Taoist class to apply stats to Water and Fire Taoists
                    if (stat.ClassId == 10)
                    {
                        Log.Debug("Applying special handling for base Taoist stats (ClassId 10) for Level {Level}", stat.Level);
                        int[] newClass = { 13, 14 }; // WaterTao and FireTao
                        foreach (var newClassId in newClass)
                        {
                            if (!Stats.ContainsKey(newClassId))
                            {
                                Stats[newClassId] = new Dictionary<int, PointAllotData>();
                                Log.Debug("Created new point allot dictionary for derived ClassId: {ClassId}", newClassId);
                            }
                            Stats[newClassId][stat.Level] = stat;
                            Log.Debug("Applied base Taoist stats to ClassId: {ClassId}, Level: {Level}",
                                newClassId, stat.Level);
                        }
                    }
                }

                Log.Information("Finished caching point allotment stats for {ClassCount} classes", Stats.Count);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to load point allotment stats from database.");
                throw;
            }
        }

        // Debug method to print all stats
        public void LogAllStats()
        {
            Log.Debug("=== All Point Allot Stats ===");
            foreach (var classKvp in Stats.OrderBy(x => x.Key))
            {
                Log.Debug("Class {ClassId}:", classKvp.Key);
                foreach (var levelKvp in classKvp.Value.OrderBy(x => x.Key))
                {
                    var stats = levelKvp.Value;
                    Log.Debug("  Level {Level}: STR={Strength}, AGI={Agility}, VIT={Vitality}, SPI={Spirit}",
                        levelKvp.Key, stats.Strength, stats.Agility, stats.Vitality, stats.Spirit);
                }
            }
        }
    }
}
