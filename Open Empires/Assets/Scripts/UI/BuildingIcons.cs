using UnityEngine;

namespace OpenEmpires
{
    public static class BuildingIcons
    {
        private static Sprite[] sprites;
        private static bool loaded;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;

            int count = 12;
            sprites = new Sprite[count];

            var names = new string[]
            {
                "houseicon",        // House = 0
                "barracksicon",     // Barracks = 1
                "towncentericon",   // TownCenter = 2
                "wallicon",         // Wall = 3
                "millicon",         // Mill = 4
                "lumberyardicon",   // LumberYard = 5
                "mineicon",         // Mine = 6
                "archeryrangeicon", // ArcheryRange = 7
                "stablesicon",      // Stables = 8
                "farmicon",         // Farm = 9
                "towericon",        // Tower = 10
                "monasteryicon"     // Monastery = 11
            };

            for (int i = 0; i < count; i++)
            {
                sprites[i] = Resources.Load<Sprite>($"BuildingIcons/{names[i]}");
                if (sprites[i] == null)
                {
                    var tex = Resources.Load<Texture2D>($"BuildingIcons/{names[i]}");
                    if (tex != null)
                        sprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
        }

        public static Sprite Get(BuildingType type)
        {
            int idx = (int)type;
            if (sprites != null && idx >= 0 && idx < sprites.Length)
                return sprites[idx];
            return null;
        }
    }
}
