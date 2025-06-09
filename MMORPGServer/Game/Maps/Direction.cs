namespace MMORPGServer.Game.Maps
{
    /// <summary>
    /// Represents a direction vector in 2D space.
    /// This struct is immutable and thread-safe.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct Direction : IEquatable<Direction>
    {
        public readonly short XChange;
        public readonly short YChange;

        public Direction(short xChange, short yChange)
        {
            XChange = xChange;
            YChange = yChange;
        }

        public static bool operator ==(Direction left, Direction right)
        {
            return left.XChange == right.XChange && left.YChange == right.YChange;
        }

        public static bool operator !=(Direction left, Direction right)
        {
            return !(left == right);
        }

        public override bool Equals(object? obj)
        {
            return obj is Direction direction && Equals(direction);
        }

        public bool Equals(Direction other)
        {
            return XChange == other.XChange && YChange == other.YChange;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(XChange, YChange);
        }

        public override string ToString()
        {
            return $"Direction: ({XChange}, {YChange})";
        }
    }
}
