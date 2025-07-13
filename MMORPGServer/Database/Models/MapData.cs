using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MMORPGServer.Database.Models
{
    public class MapData
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("describe_text")]
        public string DescribeText { get; set; }

        [Column("mapdoc")]
        public int MapDoc { get; set; } // Changed from long to int

        [Column("type")]
        public long Type { get; set; } // Changed to long for bigint

        [Column("owner_id")]
        public int OwnerId { get; set; }

        [Column("mapgroup")]
        public int MapGroup { get; set; }

        [Column("idxserver")]
        public int IdxServer { get; set; }

        [Column("weather")]
        public int Weather { get; set; }

        [Column("bgmusic")]
        public int BgMusic { get; set; }

        [Column("bgmusic_show")]
        public int BgMusicShow { get; set; }

        [Column("portal0_x")]
        public int Portal0X { get; set; }

        [Column("portal0_y")]
        public int Portal0Y { get; set; }

        [Column("reborn_map")]
        public int RebornMap { get; set; }

        [Column("reborn_portal")]
        public int RebornPortal { get; set; }

        [Column("res_lev")]
        public byte ResLev { get; set; } // Changed to byte for tinyint

        [Column("owner_type")]
        public byte OwnerType { get; set; } // Changed to byte for tinyint

        [Column("link_map")]
        public int LinkMap { get; set; }

        [Column("link_x")]
        public short LinkX { get; set; } // Changed to short for smallint

        [Column("link_y")]
        public short LinkY { get; set; } // Changed to short for smallint

        [Column("del_flag")]
        public byte DelFlag { get; set; } // Changed to byte for tinyint

        [Column("color")]
        public uint Color { get; set; } // Changed from uint to int
    }
}