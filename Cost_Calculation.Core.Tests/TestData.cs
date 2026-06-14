using System.Collections.Generic;
using Cost_Calculation.Models;

namespace Cost_Calculation.Core.Tests
{
    /// <summary>Удобные конструкторы дисков для тестов.</summary>
    internal static class TestData
    {
        public static Disc Disc(
            string setKey = "AstralVoice",
            string slot = "1",
            string main = "hp",
            int level = 15,
            string rarity = "S",
            params (string key, int upgrades)[] substats)
        {
            var disc = new Disc
            {
                SetKey = setKey,
                SlotKey = slot,
                MainStatKey = main,
                Level = level,
                Rarity = rarity,
                Substats = new List<Substat>()
            };
            foreach (var (key, upg) in substats)
                disc.Substats.Add(new Substat { Key = key, Upgrades = upg });
            return disc;
        }

        public static Substat Sub(string key, int upgrades) =>
            new() { Key = key, Upgrades = upgrades };
    }
}
