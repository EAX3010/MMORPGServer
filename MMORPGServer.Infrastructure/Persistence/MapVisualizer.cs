using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Common.Enums;
using MMORPGServer.Domain.Entities;
using MMORPGServer.Domain.ValueObjects;
using System.Drawing;
using System.Drawing.Imaging;

namespace MMORPGServer.Infrastructure.Persistence
{
    public class MapVisualizer
    {
        private readonly Dictionary<CellType, Color> _combinationColors;
        private readonly ILogger<MapVisualizer> _logger;

        public MapVisualizer(ILogger<MapVisualizer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _combinationColors = GenerateAllCombinationColors();

            _logger.LogDebug("MapVisualizer initialized with {CombinationCount} color combinations",
                _combinationColors.Count);
        }

        /// <summary>
        /// Generates a comprehensive color map for all possible flag combinations
        /// </summary>
        private Dictionary<CellType, Color> GenerateAllCombinationColors()
        {
            var colors = new Dictionary<CellType, Color>();

            // Base colors for single flags
            var baseColors = new Dictionary<CellType, Color>
            {
                [CellType.None] = Color.Gray,
                [CellType.Blocked] = Color.Black,
                [CellType.Open] = Color.White,
                [CellType.StaticObj] = Color.Green,
                [CellType.Entity] = Color.Yellow,
                [CellType.Gate] = Color.Blue,
                [CellType.BlockedObj] = Color.Red
            };

            // Single flag combinations
            foreach (var kvp in baseColors)
            {
                colors[kvp.Key] = kvp.Value;
            }

            // Generate colors for all possible combinations
            var allFlags = Enum.GetValues<CellType>().Where(f => f != CellType.None).ToArray();
            var totalCombinations = (int)Math.Pow(2, allFlags.Length);

            for (int i = 1; i < totalCombinations; i++)
            {
                var combination = CellType.None;
                for (int j = 0; j < allFlags.Length; j++)
                {
                    if ((i & (1 << j)) != 0)
                    {
                        combination |= allFlags[j];
                    }
                }

                if (!colors.ContainsKey(combination))
                {
                    colors[combination] = GenerateColorForCombination(combination, baseColors);
                }
            }

            return colors;
        }

        /// <summary>
        /// Generates a color for a specific flag combination by blending base colors
        /// </summary>
        private Color GenerateColorForCombination(CellType flags, Dictionary<CellType, Color> baseColors)
        {
            var involvedFlags = new List<CellType>();

            foreach (CellType flag in Enum.GetValues<CellType>())
            {
                if (flag != CellType.None && flags.HasFlag(flag))
                {
                    involvedFlags.Add(flag);
                }
            }

            if (involvedFlags.Count == 0)
                return Color.Gray;

            if (involvedFlags.Count == 1)
                return baseColors[involvedFlags[0]];

            // Blend colors for combinations
            return BlendColors(involvedFlags.Select(f => baseColors[f]).ToArray());
        }

        /// <summary>
        /// Blends multiple colors together
        /// </summary>
        private Color BlendColors(Color[] colors)
        {
            if (colors.Length == 0) return Color.Gray;
            if (colors.Length == 1) return colors[0];

            int r = 0, g = 0, b = 0;
            foreach (var color in colors)
            {
                r += color.R;
                g += color.G;
                b += color.B;
            }

            return Color.FromArgb(
                Math.Min(255, r / colors.Length),
                Math.Min(255, g / colors.Length),
                Math.Min(255, b / colors.Length)
            );
        }

        /// <summary>
        /// Generates a PNG image from a Map object and saves it to the specified path.
        /// </summary>
        /// <param name="map">The map to visualize.</param>
        /// <param name="outputPath">The path where the PNG file will be saved.</param>
        /// <param name="forceRegenerate">If true, regenerates even if file exists</param>
        public void GenerateMapImage(Map map, string outputPath, bool forceRegenerate = false)
        {
            // Check if file already exists
            if (!forceRegenerate && File.Exists(outputPath))
            {
                _logger.LogDebug($"Map image already exists, skipping generation: {outputPath}");
                return;
            }

            _logger.LogInformation($"Generating map image: {outputPath}");

            using Bitmap bitmap = new Bitmap(map.Width, map.Height);
            var encountereredCombinations = new HashSet<CellType>();

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Cell cell = map[x, y];
                    encountereredCombinations.Add(cell.Flags);

                    Color pixelColor = GetColorForFlags(cell.Flags);
                    bitmap.SetPixel(x, y, pixelColor);
                }
            }

            // Log all encountered combinations
            //LogEncounteredCombinations(encountereredCombinations);

            // Ensure the directory exists
            string directory = Path.GetDirectoryName(outputPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            bitmap.Save(outputPath, ImageFormat.Png);
            _logger.LogInformation($"Map image saved: {outputPath}");

            // Generate legend only if it doesn't exist or force regenerate
            string legendPath = Path.ChangeExtension(outputPath, ".legend.png");
            if (forceRegenerate || !File.Exists(legendPath))
            {
                GenerateLegend(encountereredCombinations, legendPath);
            }
            else
            {
                _logger.LogDebug($"Legend already exists, skipping: {legendPath}");
            }
        }

        /// <summary>
        /// Gets the color for specific flags, with fallback for unknown combinations
        /// </summary>
        private Color GetColorForFlags(CellType flags)
        {
            if (_combinationColors.TryGetValue(flags, out Color color))
            {
                return color;
            }

            // Fallback: generate color on the fly for unknown combinations
            _logger.LogWarning("Unknown flag combination: {Flags} ({FlagsValue})", flags, (int)flags);
            return GenerateColorForCombination(flags, new Dictionary<CellType, Color>
            {
                [CellType.Blocked] = Color.Black,
                [CellType.Open] = Color.White,
                [CellType.StaticObj] = Color.Green,
                [CellType.Entity] = Color.Yellow,
                [CellType.Gate] = Color.Blue,
                [CellType.BlockedObj] = Color.Red
            });
        }

        /// <summary>
        /// Logs all flag combinations encountered in the map
        /// </summary>
        private void LogEncounteredCombinations(HashSet<CellType> combinations)
        {
            _logger.LogInformation($"\n=== Map Analysis ===");
            _logger.LogInformation($"Total unique flag combinations found: {combinations.Count}");

            foreach (var combo in combinations.OrderBy(c => (int)c))
            {
                var flagNames = GetFlagNames(combo);
                var color = GetColorForFlags(combo);
                _logger.LogInformation($"Flags: {combo,3} ({(int)combo,2}) = [{string.Join(" | ", flagNames)}] → Color: {color.Name} (R:{color.R}, G:{color.G}, B:{color.B})");
            }
            _logger.LogInformation("==================\n");
        }

        /// <summary>
        /// Gets readable flag names for a combination
        /// </summary>
        private List<string> GetFlagNames(CellType flags)
        {
            if (flags == CellType.None)
                return new List<string> { "None" };

            var names = new List<string>();
            foreach (CellType flag in Enum.GetValues<CellType>())
            {
                if (flag != CellType.None && flags.HasFlag(flag))
                {
                    names.Add(flag.ToString());
                }
            }
            return names;
        }

        /// <summary>
        /// Generates a visual legend showing all encountered flag combinations
        /// </summary>
        private void GenerateLegend(HashSet<CellType> combinations, string legendPath)
        {
            const int swatchSize = 30;
            const int textWidth = 300;
            const int margin = 10;

            int legendWidth = swatchSize + textWidth + (margin * 3);
            int legendHeight = (combinations.Count * (swatchSize + margin)) + margin;

            using var legendBitmap = new Bitmap(legendWidth, legendHeight);
            using var graphics = Graphics.FromImage(legendBitmap);

            graphics.Clear(Color.White);

            var font = new Font("Arial", 8);
            var brush = new SolidBrush(Color.Black);

            int y = margin;
            foreach (var combo in combinations.OrderBy(c => (int)c))
            {
                // Draw color swatch
                var swatchRect = new Rectangle(margin, y, swatchSize, swatchSize);
                var swatchBrush = new SolidBrush(GetColorForFlags(combo));
                graphics.FillRectangle(swatchBrush, swatchRect);
                graphics.DrawRectangle(Pens.Black, swatchRect);

                // Draw text
                var flagNames = GetFlagNames(combo);
                var text = $"{combo} ({(int)combo}) = [{string.Join(" | ", flagNames)}]";
                graphics.DrawString(text, font, brush, swatchSize + (margin * 2), y + 8);

                swatchBrush.Dispose();
                y += swatchSize + margin;
            }

            font.Dispose();
            brush.Dispose();

            legendBitmap.Save(legendPath, ImageFormat.Png);
            _logger.LogInformation("Legend saved to: {LegendPath}", legendPath);
        }

        /// <summary>
        /// Generates a test pattern showing all possible combinations
        /// </summary>
        /// <param name="outputPath">Path where to save the test pattern</param>
        /// <param name="forceRegenerate">If true, regenerates even if file exists</param>
        public void GenerateTestPattern(string outputPath, bool forceRegenerate = false)
        {
            // Check if file already exists
            if (!forceRegenerate && File.Exists(outputPath))
            {
                _logger.LogDebug($"Test pattern already exists, skipping generation: {outputPath}");
                return;
            }

            _logger.LogDebug($"Generating test pattern: {outputPath}");

            var allCombinations = _combinationColors.Keys.OrderBy(k => (int)k).ToList();
            int gridSize = (int)Math.Ceiling(Math.Sqrt(allCombinations.Count));

            using var bitmap = new Bitmap(gridSize * 10, gridSize * 10);

            for (int i = 0; i < allCombinations.Count; i++)
            {
                int gridX = i % gridSize;
                int gridY = i / gridSize;

                var color = _combinationColors[allCombinations[i]];

                // Fill a 10x10 block for each combination
                for (int x = 0; x < 10; x++)
                {
                    for (int y = 0; y < 10; y++)
                    {
                        bitmap.SetPixel(gridX * 10 + x, gridY * 10 + y, color);
                    }
                }
            }

            bitmap.Save(outputPath, ImageFormat.Png);
            _logger.LogDebug($"Test pattern with {allCombinations.Count} combinations saved to: {outputPath}");
        }

        /// <summary>
        /// Checks if a map image already exists
        /// </summary>
        /// <param name="outputPath">Path to check</param>
        /// <returns>True if the image file exists</returns>
        public static bool MapImageExists(string outputPath)
        {
            return File.Exists(outputPath);
        }

        /// <summary>
        /// Gets the expected output path for a map ID
        /// </summary>
        /// <param name="baseDirectory">Base directory for map images</param>
        /// <param name="mapId">Map ID</param>
        /// <returns>Full path to the map image</returns>
        public static string GetMapImagePath(string baseDirectory, int mapId)
        {
            return Path.Combine(baseDirectory, $"map_{mapId}.png");
        }
    }
}