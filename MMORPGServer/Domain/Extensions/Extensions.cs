using MMORPGServer.Domain.Enums;

namespace MMORPGServer.Domain.Extensions
{
    public static class Extensions
    {
        public static string GetClientIP(this EndPoint? endPoint)
        {
            return endPoint?.ToString()?.Split(':')[0] ?? "Unknown";
        }

        public static bool IsValidPosition(this (ushort X, ushort Y) position)
        {
            return position.X > 0 && position.Y > 0 && position.X < 1000 && position.Y < 1000;
        }

        public static Orientation ToDirection(this byte value)
        {
            return (Orientation)(value % 8);
        }

        public static byte ToByte(this Orientation direction)
        {
            return (byte)direction;
        }

        public static bool IsValidCharacterName(this string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                   name.Length >= 2 &&
                   name.Length <= 16 &&
                   name.All(char.IsLetterOrDigit);
        }
    }
}