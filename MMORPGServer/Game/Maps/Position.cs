﻿namespace MMORPGServer.Game.Maps
{
    using System.Numerics;

    /// <summary>
    ///     Describes the current position of an entity.
    /// </summary>
    public struct Position
    {
        private static readonly Direction
            North,
            Northwest,
            West,
            Southwest,
            South,
            Southeast,
            East,
            Northeast;

        public Int16 X, LastX;
        public Int16 Y, LastY;

        public Object MoveSync;

        static Position()
        {
            North = new Direction(-1, -1);
            Northwest = new Direction(-1, 0);
            West = new Direction(-1, 1);
            Southwest = new Direction(0, 1);
            South = new Direction(1, 1);
            Southeast = new Direction(1, 0);
            East = new Direction(1, -1);
            Northeast = new Direction(0, -1);
        }

        public Position(Int16 x, Int16 y)
        {
            MoveSync = new object();
            X = x;
            Y = y;
            LastX = X;
            LastY = Y;
        }

        public Position(Position pos)
        {
            MoveSync = new object();
            X = pos.X;
            Y = pos.Y;
            LastX = pos.X;
            LastY = pos.Y;
        }

        /// <summary>
        ///     Returns position with X and Y being 0.
        /// </summary>
        public static Position Zero
        {
            get { return new Position(0, 0); }
        }

        /// <summary>
        ///     Returns distance between this and another position.
        /// </summary>
        /// <param name="otherPos"></param>
        /// <returns></returns>
        public Int32 GetDistance(Position otherPos)
        {
            return (Int32)Math.Sqrt(Math.Pow(X - otherPos.X, 2) + Math.Pow(Y - otherPos.Y, 2));
        }

        /// <summary>
        ///     Returns true if the other position is within range.
        /// </summary>
        /// <param name="otherPos"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        public Boolean InRange(Position otherPos, Int32 range)
        {
            return InRange(otherPos.X, otherPos.Y, range);
        }

        /// <summary>
        ///     Returns true if the other position is within range.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        public Boolean InRange(Int16 x, Int16 y, Int32 range)
        {
            return Math.Max(Math.Abs(X - x), Math.Abs(Y - y)) <= range;
        }

        /// <summary>
        ///     Returns random position around this position,
        ///     not nearer than min, and not further than max.
        /// </summary>
        /// <param name="distanceMax"></param>
        /// <param name="rnd"></param>
        /// <param name="distanceMin"></param>
        /// <returns></returns>
        public Position GetRandomLocal(Int32 distanceMin, Int32 distanceMax, Random rnd)
        {
            return GetRandom(rnd.Next(distanceMin, distanceMax + 1), rnd);
        }

        /// <summary>
        ///     Returns random position in radius around this position.
        /// </summary>
        /// <param name="radius"></param>
        /// <param name="rnd"></param>
        /// <returns></returns>
        public Position GetRandomLocal(Int32 radius, Random rnd)
        {
            return GetRandom(rnd.Next(radius + 1), rnd);
        }

        /// <summary>
        ///     Returns random position in radius around this position.
        /// </summary>
        /// <param name="distance"></param>
        /// <param name="rnd"></param>
        /// <returns></returns>
        private Position GetRandom(Int32 distance, Random rnd)
        {
            var angle = rnd.NextDouble() * Math.PI * 2;
            var x = X + distance * Math.Cos(angle);
            var y = Y + distance * Math.Sin(angle);

            return new Position((Int16)x, (Int16)y);
        }

        /// <summary>
        ///     Returns the degree facing the other position relative to Conqer's SW = N positioning system.
        /// </summary>
        public Int32 GetRelativeDegree(Position other)
        {
            double deltaX = other.X - X;
            double deltaY = other.Y - Y;

            var radian = Math.Atan2(deltaY, deltaX);
            radian -= Math.PI / 2;
            if (radian < 0) radian += Math.PI * 2;

            var degree = (Int32)(360 - (radian * 180 / Math.PI));

            return degree;
        }

        /// <summary>
        ///     Returns the Conquer relative orientation facing the other position.
        /// </summary>
        public Orientation GetOrientation(Position other)
        {
            double deltaX = other.X - X;
            double deltaY = other.Y - Y;

            var radian = Math.Atan2(deltaY, deltaX);
            radian -= Math.PI / 2;
            if (radian < 0) radian += 2 * Math.PI;

            var direction = (Orientation)(radian * 8 / (2 * Math.PI));

            return direction;
        }

        /// <summary>
        ///     Returns position on a line between this position and the other.
        /// </summary>
        /// <remarks>
        ///     When you knock back (Gale Bomb, Dash) the position is thrust in the opposite
        ///     direction. The other position would be the enemy, the distance
        ///     is how far to push it away. 
        ///     A negative distance can give a midpoint between, or a point behind this position.
        /// </remarks>
        public Position GetRelative(Position other, Int32 distance)
        {
            if (this == other)
                return this;

            double deltaX = other.X - X;
            double deltaY = other.Y - Y;

            double range = Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

            double newX = other.X + (distance / range) * (deltaX);
            double newY = other.Y + (distance / range) * (deltaY);

            return new Position((Int16)newX, (Int16)newY);
        }

        public void Restore()
        {
            var lx = LastX;
            var ly = LastY;
            Relocate(lx, ly);
        }

        public Position GetPrevious()
        {
            return new Position(LastX, LastY);
        }

        public Position Slide(Orientation movementOrientation)
        {
            lock (MoveSync)
            {
                switch (movementOrientation)
                {
                    case Orientation.Southwest:
                        return this + Southwest;
                    case Orientation.West:
                        return this + West;
                    case Orientation.Northwest:
                        return this + Northwest;
                    case Orientation.North:
                        return this + North;
                    case Orientation.Northeast:
                        return this + Northeast;
                    case Orientation.East:
                        return this + East;
                    case Orientation.Southeast:
                        return this + Southeast;
                    case Orientation.South:
                        return this + South;
                }
                return this;
            }
        }

        public void Relocate(Position pos)
        {
            Relocate(pos.X, pos.Y);
        }

        public void Relocate(Int16 x, Int16 y)
        {
            lock (MoveSync)
            {
                LastX = X;
                X = x;
                LastY = Y;
                Y = y;
            }
        }

        public static Boolean operator ==(Position pos1, Position pos2)
        {
            return (pos1.X == pos2.X && pos1.Y == pos2.Y);
        }

        public static Boolean operator !=(Position pos1, Position pos2)
        {
            return !(pos1 == pos2);
        }

        public static Position operator +(Position change, Direction direction)
        {
            lock (change.MoveSync)
            {
                change.LastX = change.X;
                change.LastY = change.Y;
                change.X += direction.XChange;
                change.Y += direction.YChange;
                return change;
            }
        }

        public static Int32 operator -(Position pos1, Position pos2)
        {
            return pos1.GetDistance(pos2);
        }

        public override Int32 GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode();
        }

        public override Boolean Equals(object obj)
        {
            return obj is Position && this == (Position)obj;
        }

        public override String ToString()
        {
            return $"Position: {X}, {Y}";
        }

        // Conversion to Vector2
        public Vector2 ToVector2() => new Vector2(X, Y);

        // Conversion from Vector2
        public static Position FromVector2(Vector2 v) => new Position((short)v.X, (short)v.Y);

        // Implicit conversion to Vector2
        public static implicit operator Vector2(Position p) => new Vector2(p.X, p.Y);

        // Explicit conversion from Vector2
        public static explicit operator Position(Vector2 v) => new Position((short)v.X, (short)v.Y);
    }
}