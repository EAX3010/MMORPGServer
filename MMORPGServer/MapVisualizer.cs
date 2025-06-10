using System.Drawing;
using System.Drawing.Imaging;
namespace MMORPGServer.Interfaces
{
    public static class MapVisualizer
    {
        /// <summary>
        /// Generates a PNG image from a Map object and saves it to the specified path.
        /// </summary>
        /// <param name="map">The map to visualize.</param>
        /// <param name="outputPath">The path where the PNG file will be saved.</param>
        public static void GenerateMapImage(Map map, string outputPath)
        {
            // Define colors for each cell type
            var colorMap = new Dictionary<CellType, Color>
            {
                [CellType.Open] = Color.White, // Beige
                [CellType.Blocked] = Color.Black,    // Blue
                [CellType.StaticObj] = Color.Red,   // Red
                [CellType.BlockedObj] = Color.Green,   // Red
                [CellType.Portal] = Color.SkyBlue,   // Red
                [CellType.Entity] = Color.Brown,   // Red
            };

            // Create a bitmap with the map's dimensions
            using var bitmap = new Bitmap(map.Width, map.Height);

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Cell cell = map[x, y];
                    Color pixelColor;

                    // Prioritize colors for visualization
                    if (cell.Flags.HasFlag(CellType.BlockedObj))
                    {
                        pixelColor = colorMap[CellType.BlockedObj];
                    }
                    else if (cell.Flags.HasFlag(CellType.StaticObj))
                    {
                        pixelColor = colorMap[CellType.StaticObj];
                    }

                    else if (cell.Flags.HasFlag(CellType.Entity))
                    {
                        pixelColor = colorMap[CellType.Entity];
                    }
                    else if (cell.Flags.HasFlag(CellType.Portal))
                    {
                        pixelColor = colorMap[CellType.Portal];
                    }
                    else if (cell.Flags.HasFlag(CellType.Blocked))
                    {
                        pixelColor = colorMap[CellType.Blocked];
                    }
                    else if (cell.Flags.HasFlag(CellType.Open))
                    {
                        pixelColor = colorMap[CellType.Open];
                    }
                    else
                    {
                        pixelColor = Color.Brown;
                    }
                    bitmap.SetPixel(x, y, pixelColor);
                }
            }

            // Ensure the directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Save the bitmap as a PNG file
            bitmap.Save(outputPath, ImageFormat.Png);
        }
    }
}