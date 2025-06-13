using MMORPGServer.Domain.Enums;

namespace MMORPGServer.Domain.ValueObjects
{
    /// <summary>
    /// Represents a cell in the game map with its properties and flags.
    /// This struct is immutable and thread-safe.
    /// </summary>
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly CellType Flags;

        /// <summary>
        ///     Holds altitude for valid tiles. Jump height difference limit is 200.
        ///     If the tile is flagged as a portal, this will be the portal's destination ID.
        /// </summary>
        public readonly ushort Argument;
        public readonly ushort CellFlag;

        public Cell(CellType baseType, ushort altitude, ushort cellFlag)
        {
            Flags = baseType;
            Argument = altitude;
            CellFlag = cellFlag;
        }

        public bool this[CellType flag]
        {
            get { return Flags.HasFlag(flag); }
        }

        public Cell AddFlag(CellType flag)
        {
            return new Cell(Flags | flag, Argument, CellFlag);
        }

        public Cell RemoveFlag(CellType flag)
        {
            return new Cell(Flags & ~flag, Argument, CellFlag);
        }

        public Cell SetArgument(ushort value)
        {
            return new Cell(Flags, value, CellFlag);
        }

        public static implicit operator bool(Cell cell)
        {
            return cell[CellType.Open];
        }

        public static bool operator ==(Cell left, Cell right)
        {
            return left.Flags == right.Flags &&
                   left.Argument == right.Argument &&
                   left.CellFlag == right.CellFlag;
        }

        public static bool operator !=(Cell left, Cell right)
        {
            return !(left == right);
        }

        public override bool Equals(object obj)
        {
            return obj is Cell cell && Equals(cell);
        }

        public bool Equals(Cell other)
        {
            return Flags == other.Flags &&
                   Argument == other.Argument &&
                   CellFlag == other.CellFlag;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Flags, Argument, CellFlag);
        }

        public override string ToString()
        {
            return $"Flags: {Flags} | Argument: {Argument} | Flag: {CellFlag}";
        }
    }
}