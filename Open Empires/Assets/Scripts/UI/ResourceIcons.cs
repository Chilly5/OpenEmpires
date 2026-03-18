using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace OpenEmpires
{
    public static class ResourceIcons
    {
        private static Sprite food, wood, gold, stone;
        private static TMP_SpriteAsset spriteAsset;
        private static bool loaded;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;

            food = Resources.Load<Sprite>("ResourceIcons/foodicon");
            wood = Resources.Load<Sprite>("ResourceIcons/woodicon");
            gold = Resources.Load<Sprite>("ResourceIcons/goldicon");
            stone = Resources.Load<Sprite>("ResourceIcons/stoneicon");

            // If sprites loaded as Sprite are null, try loading as Texture2D and create sprites
            if (food == null)
            {
                var foodTex = Resources.Load<Texture2D>("ResourceIcons/foodicon");
                if (foodTex != null) food = Sprite.Create(foodTex, new Rect(0, 0, foodTex.width, foodTex.height), new Vector2(0.5f, 0.5f));
            }
            if (wood == null)
            {
                var woodTex = Resources.Load<Texture2D>("ResourceIcons/woodicon");
                if (woodTex != null) wood = Sprite.Create(woodTex, new Rect(0, 0, woodTex.width, woodTex.height), new Vector2(0.5f, 0.5f));
            }
            if (gold == null)
            {
                var goldTex = Resources.Load<Texture2D>("ResourceIcons/goldicon");
                if (goldTex != null) gold = Sprite.Create(goldTex, new Rect(0, 0, goldTex.width, goldTex.height), new Vector2(0.5f, 0.5f));
            }
            if (stone == null)
            {
                var stoneTex = Resources.Load<Texture2D>("ResourceIcons/stoneicon");
                if (stoneTex != null) stone = Sprite.Create(stoneTex, new Rect(0, 0, stoneTex.width, stoneTex.height), new Vector2(0.5f, 0.5f));
            }

            BuildSpriteAsset();
        }

        public static Sprite Get(ResourceType type) => type switch
        {
            ResourceType.Food => food,
            ResourceType.Wood => wood,
            ResourceType.Gold => gold,
            ResourceType.Stone => stone,
            _ => null
        };

        private static void BuildSpriteAsset()
        {
            // Pack the 4 icons into a single atlas texture for TMP_SpriteAsset
            var sprites = new (string name, Sprite sprite)[]
            {
                ("food", food),
                ("wood", wood),
                ("gold", gold),
                ("stone", stone)
            };

            // Determine atlas size — place icons in a 2x2 grid, each cell = max icon size
            int cellSize = 0;
            foreach (var (_, s) in sprites)
            {
                if (s == null) continue;
                if (s.texture.width > cellSize) cellSize = s.texture.width;
                if (s.texture.height > cellSize) cellSize = s.texture.height;
            }
            if (cellSize == 0) cellSize = 64;

            int atlasSize = cellSize * 2;
            var atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false);
            atlas.name = "ResourceIconsAtlas";
            // Clear to transparent
            var clear = new Color32[atlasSize * atlasSize];
            atlas.SetPixels32(clear);

            // Positions: top-left(0), top-right(1), bottom-left(2), bottom-right(3)
            var positions = new Vector2Int[]
            {
                new Vector2Int(0, cellSize),          // food — top-left
                new Vector2Int(cellSize, cellSize),    // wood — top-right
                new Vector2Int(0, 0),                  // gold — bottom-left
                new Vector2Int(cellSize, 0)            // stone — bottom-right
            };

            for (int i = 0; i < sprites.Length; i++)
            {
                var s = sprites[i].sprite;
                if (s == null) continue;

                var srcTex = GetReadableTexture(s.texture);
                var pixels = srcTex.GetPixels32();
                int w = srcTex.width;
                int h = srcTex.height;

                // Copy pixels into atlas at the correct position
                for (int y = 0; y < h && y < cellSize; y++)
                {
                    for (int x = 0; x < w && x < cellSize; x++)
                    {
                        atlas.SetPixel(positions[i].x + x, positions[i].y + y, pixels[y * w + x]);
                    }
                }
            }

            atlas.Apply();

            // Create TMP_SpriteAsset
            spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            spriteAsset.name = "ResourceIcons";
            spriteAsset.spriteSheet = atlas;
            spriteAsset.material = new Material(Shader.Find("TextMeshPro/Sprite"));
            spriteAsset.material.mainTexture = atlas;

            // Build sprite glyph + character tables
            var glyphTable = new List<TMP_SpriteGlyph>();
            var charTable = new List<TMP_SpriteCharacter>();

            for (int i = 0; i < sprites.Length; i++)
            {
                var s = sprites[i].sprite;
                int w = s != null ? s.texture.width : cellSize;
                int h = s != null ? s.texture.height : cellSize;
                if (w > cellSize) w = cellSize;
                if (h > cellSize) h = cellSize;

                var glyph = new TMP_SpriteGlyph
                {
                    index = (uint)i,
                    metrics = new GlyphMetrics(w, h, 0, h * 0.8f, w),
                    glyphRect = new GlyphRect(positions[i].x, positions[i].y, w, h),
                    scale = 1.0f
                };
                glyphTable.Add(glyph);

                var character = new TMP_SpriteCharacter
                {
                    name = sprites[i].name,
                    glyphIndex = (uint)i,
                    glyph = glyph,
                    scale = 1.0f
                };
                charTable.Add(character);
            }

            // Set version to "1.1.0" to prevent UpgradeSpriteAsset from running legacy migration
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var versionField = typeof(TMP_SpriteAsset).GetField("m_Version", flags);
            if (versionField != null) versionField.SetValue(spriteAsset, "1.1.0");

            // Populate glyph table (getter is safe — no side effects)
            spriteAsset.spriteGlyphTable.AddRange(glyphTable);

            // Character table getter triggers UpdateLookupTables which crashes on fresh assets,
            // so populate via the backing field directly
            var charField = typeof(TMP_SpriteAsset).GetField("m_SpriteCharacterTable", flags);
            ((List<TMP_SpriteCharacter>)charField.GetValue(spriteAsset)).AddRange(charTable);

            spriteAsset.UpdateLookupTables();

            // Register as default so all TMP_Text can use <sprite name="food"> etc.
            if (TMP_Settings.defaultSpriteAsset == null)
            {
                TMP_Settings.defaultSpriteAsset = spriteAsset;
            }
            else
            {
                // Add as fallback to the existing default
                var existing = TMP_Settings.defaultSpriteAsset;
                if (existing.fallbackSpriteAssets == null)
                    existing.fallbackSpriteAssets = new List<TMP_SpriteAsset>();
                existing.fallbackSpriteAssets.Add(spriteAsset);
            }
        }

        private static Texture2D GetReadableTexture(Texture2D source)
        {
            // GPU readback via RenderTexture for non-readable textures
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }
    }
}
