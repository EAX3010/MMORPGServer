using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
namespace MMORPGServer.Infrastructure
{
    public class MapVisualizer()
    {
        /// <summary>
        /// Generates a PNG image from a Map object and saves it to the specified path.
        /// </summary>
        /// <param name="map">The map to visualize.</param>
        /// <param name="outputPath">The path where the PNG file will be saved.</param>
        public void GenerateMapImage(Map map, string outputPath)
        {
            // Define colors for each cell type
            Dictionary<CellType, Color> colorMap = new Dictionary<CellType, Color>
            {
                [CellType.Open] = Color.White,
                [CellType.Blocked] = Color.Black,
                [CellType.StaticObj] = Color.Green,
                [CellType.BlockedObj] = Color.DarkRed,
                [CellType.Portal] = Color.SkyBlue,
                [CellType.Entity] = Color.Blue,
            };

            // Create a bitmap with the map's dimensions
            using Bitmap bitmap = new Bitmap(map.Width, map.Height);

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
                        pixelColor = Color.Gray; // Default color for unknown flags
                        Console.WriteLine($"Unknown map flag {cell.Flags}");
                    }
                    bitmap.SetPixel(x, y, pixelColor);
                }
            }

            // Ensure the directory exists
            string directory = Path.GetDirectoryName(outputPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            if (!File.Exists(outputPath))
                bitmap.Save(outputPath, ImageFormat.Png);
        }
    }
}