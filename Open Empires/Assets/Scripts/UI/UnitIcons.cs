using UnityEngine;

namespace OpenEmpires
{
    public static class UnitIcons
    {
        private static Sprite[] sprites;
        private static bool loaded;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;

            int count = 13;
            sprites = new Sprite[count];

            var names = new string[]
            {
                "villagericon",      // Villager = 0
                "spearmanicon",      // Spearman = 1
                "archericon",        // Archer = 2
                "horsemanicon",      // Horseman = 3
                "scouticon",         // Scout = 4
                null,                // Sheep = 5 (no icon)
                "manatarmsicon",     // Man-at-Arms = 6
                "knighticon",        // Knight = 7
                "crossbowmanicon",   // Crossbowman = 8
                "monkicon",          // Monk = 9
                "archericon",        // Longbowman = 10 (reuses archer icon)
                "horsemanicon",      // Gendarme = 11 (reuses horseman icon)
                "spearmanicon"       // Landsknecht = 12 (reuses spearman icon)
            };

            for (int i = 0; i < count; i++)
            {
                if (names[i] == null) continue;
                sprites[i] = Resources.Load<Sprite>($"UnitIcons/{names[i]}");
                if (sprites[i] == null)
                {
                    var tex = Resources.Load<Texture2D>($"UnitIcons/{names[i]}");
                    if (tex != null)
                        sprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
        }

        public static Sprite Get(int unitType)
        {
            if (sprites != null && unitType >= 0 && unitType < sprites.Length)
                return sprites[unitType];
            return null;
        }
    }
}
