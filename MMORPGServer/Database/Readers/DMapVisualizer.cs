using MMORPGServer.Common.Enums;
using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Entities;
using Serilog;
using System.Drawing;
using System.Drawing.Imaging;

namespace MMORPGServer.Database.Readers
{
    public class DMapVisualizer
    {
        private readonly Dictionary<CellType, Color> _combinationColors;

        public DMapVisualizer()
        {
            _combinationColors = GenerateAllCombinationColors();

            Log.Debug("DMapVisualizer initialized with {CombinationCount} color combinations",
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
                    if ((i & 1 << j) != 0)
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
        public void GenerateMapImage(DMap map, string outputPath, bool forceRegenerate = false)
        {
            try
            {
                // Check if file already exists
                if (!forceRegenerate && File.Exists(outputPath))
                {
                    Log.Debug("Map image for DMap {DMapId} already exists, skipping generation: {OutputPath}", map.Id, outputPath);
                    return;
                }

                Log.Debug("Generating map image for DMap {DMapId}: {OutputPath}", map.Id, outputPath);

                using var bitmap = new Bitmap(map.Width, map.Height);
                var encounteredCombinations = new HashSet<CellType>();

                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        Cell cell = map[x, y];
                        encounteredCombinations.Add(cell.Flags);

                        Color pixelColor = GetColorForFlags(cell.Flags);
                        bitmap.SetPixel(x, y, pixelColor);
                    }
                }

                // Ensure the directory exists
                string? directory = Path.GetDirectoryName(outputPath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Log.Debug("Created directory for map images: {Directory}", directory);
                }

                bitmap.Save(outputPath, ImageFormat.Png);
                Log.Debug("Map image saved for DMap {DMapId}: {OutputPath}", map.Id, outputPath);

                // Generate legend only if it doesn't exist or force regenerate
                string legendPath = Path.ChangeExtension(outputPath, ".legend.png");
                if (forceRegenerate || !File.Exists(legendPath))
                {
                    GenerateLegend(encounteredCombinations, legendPath);
                }
                else
                {
                    Log.Debug("Legend already exists for DMap {DMapId}, skipping: {LegendPath}", map.Id, legendPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to generate map image for DMap {DMapId} at {OutputPath}", map?.Id, outputPath);
                throw;
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
            Log.Warning("Unknown flag combination encountered: {Flags} ({FlagsValue})", flags, (int)flags);
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
            Log.Debug("=== Map Analysis ===");
            Log.Debug("Total unique flag combinations found: {CombinationCount}", combinations.Count);

            foreach (var combo in combinations.OrderBy(c => (int)c))
            {
                var flagNames = GetFlagNames(combo);
                var color = GetColorForFlags(combo);
                Log.Debug("Flags: {Combo} ({ComboValue}) = [{FlagNames}] → Color: {ColorName} (R:{R}, G:{G}, B:{B})",
                    combo, (int)combo, string.Join(" | ", flagNames), color.Name, color.R, color.G, color.B);
            }
            Log.Debug("==================");
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
            try
            {
                const int swatchSize = 30;
                const int textWidth = 300;
                const int margin = 10;

                int legendWidth = swatchSize + textWidth + margin * 3;
                int legendHeight = combinations.Count * (swatchSize + margin) + margin;

                using var legendBitmap = new Bitmap(legendWidth, legendHeight);
                using var graphics = Graphics.FromImage(legendBitmap);

                graphics.Clear(Color.White);

                using var font = new Font("Arial", 8);
                using var brush = new SolidBrush(Color.Black);

                int y = margin;
                foreach (var combo in combinations.OrderBy(c => (int)c))
                {
                    // Draw color swatch
                    var swatchRect = new Rectangle(margin, y, swatchSize, swatchSize);
                    using var swatchBrush = new SolidBrush(GetColorForFlags(combo));
                    graphics.FillRectangle(swatchBrush, swatchRect);
                    graphics.DrawRectangle(Pens.Black, swatchRect);

                    // Draw text
                    var flagNames = GetFlagNames(combo);
                    var text = $"{combo} ({(int)combo}) = [{string.Join(" | ", flagNames)}]";
                    graphics.DrawString(text, font, brush, swatchSize + margin * 2, y + 8);

                    y += swatchSize + margin;
                }

                legendBitmap.Save(legendPath, ImageFormat.Png);
                Log.Debug("Legend saved to: {LegendPath}", legendPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to generate legend: {LegendPath}", legendPath);
            }
        }

        /// <summary>
        /// Generates a test pattern showing all possible combinations
        /// </summary>
        /// <param name="outputPath">Path where to save the test pattern</param>
        /// <param name="forceRegenerate">If true, regenerates even if file exists</param>
        public void GenerateTestPattern(string outputPath, bool forceRegenerate = false)
        {
            try
            {
                // Check if file already exists
                if (!forceRegenerate && File.Exists(outputPath))
                {
                    Log.Debug("Test pattern already exists, skipping generation: {OutputPath}", outputPath);
                    return;
                }

                Log.Debug("Generating test pattern: {OutputPath}", outputPath);

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

                // Ensure directory exists
                string? directory = Path.GetDirectoryName(outputPath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                bitmap.Save(outputPath, ImageFormat.Png);
                Log.Debug("Test pattern with {CombinationCount} combinations saved to: {OutputPath}",
                    allCombinations.Count, outputPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to generate test pattern: {OutputPath}", outputPath);
            }
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

        /// <summary>
        /// Gets the total number of color combinations available
        /// </summary>
        public int GetCombinationCount()
        {
            return _combinationColors.Count;
        }

        /// <summary>
        /// Gets all available color combinations for debugging
        /// </summary>
        public IReadOnlyDictionary<CellType, Color> GetAllCombinations()
        {
            return _combinationColors.AsReadOnly();
        }
    }
}
