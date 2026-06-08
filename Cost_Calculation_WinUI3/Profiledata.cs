using System;
using System.Collections.Generic;
using Cost_Calculation.Models;

namespace Cost_Calculation
{
    public class ProfileData
    {
        public string Name { get; set; } = "";
        public DiscExport Export { get; set; } = null;
        public HashSet<int> MarkedIds { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;

        public bool IsEmpty => Export == null || Export.discs == null || Export.discs.Count == 0;
    }
}