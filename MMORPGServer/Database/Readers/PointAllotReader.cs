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
        public PointAllotData this[ClassType classId, int level]
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
                return null;
            }
        }

        // Load all class/level stats from database
        public async Task LoadAllStatsAsync()
        {
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
                    Log.Debug("Created new class dictionary for ClassId: {ClassId}", stat.ClassId);
                }
                Stats[stat.ClassId][stat.Level] = stat;
                Log.Debug("Added ClassId: {ClassId}, Level: {Level}, STR: {Strength}, AGI: {Agility}, VIT: {Vitality}, SPI: {Spirit}",
                    stat.ClassId, stat.Level, stat.Strength, stat.Agility, stat.Vitality, stat.Spirit);

                if (stat.ClassId == 10)
                {
                    Log.Debug("Special handling for ClassId 100: {Level} - STR: {Strength}, AGI: {Agility}, VIT: {Vitality}, SPI: {Spirit}",
                        stat.Level, stat.Strength, stat.Agility, stat.Vitality, stat.Spirit);
                    int[] newClass = [13, 14];
                    foreach (var newClassId in newClass)
                    {
                        if (!Stats.ContainsKey(newClassId))
                        {
                            Stats[newClassId] = new Dictionary<int, PointAllotData>();
                            Log.Debug("Created new class dictionary for ClassId: {ClassId}", newClassId);
                        }
                        Stats[newClassId][stat.Level] = stat;
                        Log.Debug("Added ClassId: {ClassId}, Level: {Level}, STR: {Strength}, AGI: {Agility}, VIT: {Vitality}, SPI: {Spirit}",
                            newClassId, stat.Level, stat.Strength, stat.Agility, stat.Vitality, stat.Spirit);
                    }
                }
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