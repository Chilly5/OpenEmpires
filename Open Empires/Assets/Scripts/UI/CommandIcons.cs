using UnityEngine;

namespace OpenEmpires
{
    public static class CommandIcons
    {
        private static Sprite attack, guard, patrol, garrison, chop, mine, build;
        private static bool loaded;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;

            attack = LoadIcon("attackicon");
            guard = LoadIcon("guardicon");
            patrol = LoadIcon("patrolicon");
            garrison = LoadIcon("garrisonicon");
            chop = LoadIcon("chopicon");
            mine = LoadIcon("mineicon");
            build = LoadIcon("buildicon");
        }

        private static Sprite LoadIcon(string name)
        {
            var sprite = Resources.Load<Sprite>($"ResourceIcons/{name}");
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>($"ResourceIcons/{name}");
                if (tex != null)
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            return sprite;
        }

        public static Sprite Attack => attack;
        public static Sprite Guard => guard;
        public static Sprite Patrol => patrol;
        public static Sprite Garrison => garrison;
        public static Sprite Chop => chop;
        public static Sprite Mine => mine;
        public static Sprite Build => build;
    }
}
