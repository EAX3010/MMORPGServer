namespace MMORPGServer.Game.Maps
{
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Cell
    {
        public CellType Flags;

        /// <summary>
        ///     Holds altitude for valid tiles. Jump height difference limit is 200.
        ///     If the tile is flagged as a portal, this will be the portal's destination ID.
        /// </summary>
        public ushort Argument;
        public ushort CellFlag;
        public Cell(CellType baseType, ushort altitude, ushort cellFlag)
        {
            Flags = baseType;
            Argument = altitude;
            CellFlag = cellFlag;
        }

        public static implicit operator bool(Cell cell)
        {
            return cell[CellType.Open];
        }

        public bool this[CellType flag]
        {
            get { return (Flags & flag) == flag; }
            set
            {
                if (value)
                    Flags |= flag;
                else
                    Flags &= ~flag;
            }
        }

        public Cell AddFlag(CellType flag)
        {
            this[flag] = true;
            return this;
        }

        public Cell RemoveFlag(CellType flag)
        {
            this[flag] = false;
            return this;
        }

        public Cell SetArgument(ushort value)
        {
            Argument = value;
            return this;
        }

        public override string ToString()
        {
            return $"Flags: {Flags} | Argument: {Argument} | Flag: {CellFlag}";
        }
    }
}