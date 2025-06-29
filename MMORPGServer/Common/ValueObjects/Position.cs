using MMORPGServer.Common.Enums;
using System.Numerics;
using System.Runtime.InteropServices;

namespace MMORPGServer.Common.ValueObjects
{
    /// <summary>
    ///     Represents a 2D position in the game world.
    ///     This struct is immutable and thread-safe.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct Position : IEquatable<Position>
    {
        private static readonly Direction[] Directions = new[]
        {
            new Direction(-1, -1),  // North
            new Direction(-1, 0),   // Northwest
            new Direction(-1, 1),   // West
            new Direction(0, 1),    // Southwest
            new Direction(1, 1),    // South
            new Direction(1, 0),    // Southeast
            new Direction(1, -1),   // East
            new Direction(0, -1)    // Northeast
        };

        public readonly short X;
        public readonly short Y;
        public readonly short LastX;
        public readonly short LastY;

        public Position(short x, short y)
        {
            X = x;
            Y = y;
            LastX = x;
            LastY = y;
        }

        public Position(short x, short y, short lastX, short lastY)
        {
            X = x;
            Y = y;
            LastX = lastX;
            LastY = lastY;
        }

        public static Position Zero => new(0, 0);

        public double GetDistance(Position other)
        {
            return Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
        }

        public bool InRange(Position other, int range)
        {
            return InRange(other.X, other.Y, range);
        }

        public bool InRange(short x, short y, int range)
        {
            return Math.Max(Math.Abs(X - x), Math.Abs(Y - y)) <= range;
        }

        public Position GetRandomLocal(int distanceMin, int distanceMax, Random rnd)
        {
            return GetRandom(rnd.Next(distanceMin, distanceMax + 1), rnd);
        }

        public Position GetRandomLocal(int radius, Random rnd)
        {
            return GetRandom(rnd.Next(radius + 1), rnd);
        }

        private Position GetRandom(int distance, Random rnd)
        {
            double angle = rnd.NextDouble() * Math.PI * 2;
            double x = X + distance * Math.Cos(angle);
            double y = Y + distance * Math.Sin(angle);

            return new Position((short)x, (short)y);
        }

        public int GetRelativeDegree(Position other)
        {
            double deltaX = other.X - X;
            double deltaY = other.Y - Y;

            double radian = Math.Atan2(deltaY, deltaX);
            radian -= Math.PI / 2;
            if (radian < 0) radian += Math.PI * 2;

            return (int)(360 - radian * 180 / Math.PI);
        }

        public Orientation GetOrientation(Position other)
        {
            double deltaX = other.X - X;
            double deltaY = other.Y - Y;

            double radian = Math.Atan2(deltaY, deltaX);
            radian -= Math.PI / 2;
            if (radian < 0) radian += 2 * Math.PI;

            return (Orientation)(radian * 8 / (2 * Math.PI));
        }

        public Position GetRelative(Position other, int distance)
        {
            if (this == other)
                return this;

            double deltaX = other.X - X;
            double deltaY = other.Y - Y;

            double range = Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

            double newX = other.X + distance / range * deltaX;
            double newY = other.Y + distance / range * deltaY;

            return new Position((short)newX, (short)newY);
        }

        public Position GetPrevious()
        {
            return new Position(LastX, LastY);
        }

        public Position Slide(Orientation movementOrientation)
        {
            Direction direction = Directions[(int)movementOrientation];
            return new Position(
                (short)(X + direction.XChange),
                (short)(Y + direction.YChange),
                X,
                Y
            );
        }

        public static bool operator ==(Position left, Position right)
        {
            return left.X == right.X && left.Y == right.Y;
        }

        public static bool operator !=(Position left, Position right)
        {
            return !(left == right);
        }

        public static Position operator +(Position position, Direction direction)
        {
            return new Position(
                (short)(position.X + direction.XChange),
                (short)(position.Y + direction.YChange),
                position.X,
                position.Y
            );
        }

        public static double operator -(Position left, Position right)
        {
            return left.GetDistance(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override bool Equals(object obj)
        {
            return obj is Position position && Equals(position);
        }

        public bool Equals(Position other)
        {
            return X == other.X && Y == other.Y;
        }

        public override string ToString()
        {
            return $"Position: {X}, {Y}";
        }

        public Vector2 ToVector2() => new(X, Y);

        public static Position FromVector2(Vector2 v) => new((short)v.X, (short)v.Y);

        public static implicit operator Vector2(Position p) => new(p.X, p.Y);

        public static explicit operator Position(Vector2 v) => new((short)v.X, (short)v.Y);
    }
}