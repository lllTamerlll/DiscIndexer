using System.Collections.Generic;

namespace Cost_Calculation.Models
{
    public class DiscExport
    {
        public List<Disc> discs { get; set; } = new List<Disc>();
        public string format { get; set; }
        public int version { get; set; }
        public string source { get; set; }
    }

    public class Disc
    {
        public int Id { get; set; }
        public string setKey { get; set; }
        public string slotKey { get; set; }
        public int level { get; set; }
        public string rarity { get; set; }
        public string mainStatKey { get; set; }
        public List<Substat> substats { get; set; } = new List<Substat>();
    }

    public class Substat
    {
        public string key { get; set; }
        public int upgrades { get; set; }
    }
}