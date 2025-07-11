using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MMORPGServer.Database.Models
{
    public class PointAllotData
    {
        [Key]
        public int Id { get; set; }

        [Column("profession")]
        public int ClassId { get; set; }

        public int Level { get; set; }

        [Column("force")]
        public int Strength { get; set; }

        [Column("speed")]
        public int Agility { get; set; }

        [Column("health")]
        public int Vitality { get; set; }

        [Column("soul")]
        public int Spirit { get; set; }

    }
}
