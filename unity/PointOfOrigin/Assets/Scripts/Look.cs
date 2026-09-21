using System.Collections.Generic;
using UnityEngine;

namespace PointOfOrigin
{
    /// <summary>
    /// The wanderer's look: a robe colour, a hat and a lantern tint, chosen on the Wanderer page and
    /// saved with progress. The sprite is a flat-shaded Blender render, so the robe and the lantern are
    /// recoloured on the loaded pixels and the hats are drawn over the head; nothing is re-rendered.
    /// More choices unlock as origins are found, and one lantern comes from the place the road does
    /// not lead to.
    /// </summary>
    public static class Look
    {
        public struct Option
        {
            public string name;
            public Color32 colour;
            public int needs;          // origins found before it opens; -1 = the secret
        }

        public static readonly Option[] Robes =
        {
            new Option { name = "Linen", colour = new Color32(255, 255, 255, 255), needs = 0 },
            new Option { name = "Moss", colour = new Color32(120, 170, 110, 255), needs = 0 },
            new Option { name = "Ember", colour = new Color32(220, 105, 80, 255), needs = 0 },
            new Option { name = "Deep water", colour = new Color32(95, 130, 200, 255), needs = 2 },
            new Option { name = "Dusk", colour = new Color32(160, 110, 190, 255), needs = 4 },
            new Option { name = "Gold", colour = new Color32(235, 190, 80, 255), needs = 6 },
            new Option { name = "Charcoal", colour = new Color32(80, 80, 92, 255), needs = 8 },
        };

        public static readonly Option[] Hats =
        {
            new Option { name = "Bare head", needs = 0 },
            new Option { name = "Cap", needs = 3 },
            new Option { name = "Pointed hat", needs = 6 },
            new Option { name = "Crown", needs = 10 },
        };

        public static readonly Option[] Lanterns =
        {
            new Option { name = "Warm", colour = new Color32(255, 255, 255, 255), needs = 0 },
            new Option { name = "Cold", colour = new Color32(150, 200, 255, 255), needs = 5 },
            new Option { name = "First light", colour = new Color32(180, 255, 170, 255), needs = -1 },
            new Option { name = "Violet", colour = new Color32(210, 150, 255, 255), needs = 10 },
        };

        const string KeyRobe = "po.look.robe", KeyHat = "po.look.hat", KeyLantern = "po.look.lantern";

        public static int Robe { get => PlayerPrefs.GetInt(KeyRobe, 0); set { PlayerPrefs.SetInt(KeyRobe, value); PlayerPrefs.Save(); } }
        public static int Hat { get => PlayerPrefs.GetInt(KeyHat, 0); set { PlayerPrefs.SetInt(KeyHat, value); PlayerPrefs.Save(); } }
        public static int Lantern { get => PlayerPrefs.GetInt(KeyLantern, 0); set { PlayerPrefs.SetInt(KeyLantern, value); PlayerPrefs.Save(); } }

        public static bool Open(Option o, int found, bool secret) => o.needs < 0 ? secret : found >= o.needs;

        public static string Requirement(Option o) =>
            o.needs < 0 ? "found where the road does not lead" : o.needs == 1 ? "one origin found" : $"{o.needs} origins found";

        /// <summary>A locked choice falls back to the first open one, so a wiped save never wears a crown.</summary>
        public static void Settle(int found, bool secret)
        {
            if (!Open(Robes[Mathf.Clamp(Robe, 0, Robes.Length - 1)], found, secret)) Robe = 0;
            if (!Open(Hats[Mathf.Clamp(Hat, 0, Hats.Length - 1)], found, secret)) Hat = 0;
            if (!Open(Lanterns[Mathf.Clamp(Lantern, 0, Lanterns.Length - 1)], found, secret)) Lantern = 0;
        }

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>The wanderer frame with the chosen robe, hat and lantern. Frames are 48x64 renders: the head fills rows 7 to 24 of columns 14 to 33 (top-down), the lantern hangs to the right.</summary>
        public static Texture2D Build(Texture2D source, int robe, int hat, int lantern)
        {
            string key = $"{source.name}:{robe}:{hat}:{lantern}";
            if (cache.TryGetValue(key, out var done) && done != null) return done;
            int w = source.width, h = source.height;
            var px = ReadPixels(source);
            var robeCol = Robes[Mathf.Clamp(robe, 0, Robes.Length - 1)].colour;
            var lanternCol = Lanterns[Mathf.Clamp(lantern, 0, Lanterns.Length - 1)].colour;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a < 40) continue;
                int x = i % w, yTop = h - 1 - i / w;   // GetPixels32 is bottom-up; the layout notes are top-down
                int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                if (max < 110) continue;                                             // the outline
                bool skin = yTop < 26 && c.r > 200 && c.g > 180 && c.g < 235 && c.b > 110 && c.b < 175;
                if (skin) continue;
                bool warm = c.r > c.g && c.g > c.b && c.r - c.b > 40 && yTop >= 26;   // the lantern's light
                if (warm) { px[i] = Tint(c, lanternCol); continue; }
                if (max - min < 48) px[i] = Tint(c, robeCol);                         // the robe: the pale cloth
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = key };
            tex.SetPixels32(px);
            if (hat > 0) DrawHat(tex, hat, robeCol);
            tex.Apply();
            cache[key] = tex;
            return tex;
        }

        /// <summary>The pixels of a texture that may not be marked readable: through a render texture when it is not.</summary>
        static Color32[] ReadPixels(Texture2D src)
        {
            if (src.isReadable) return src.GetPixels32();
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tmp = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tmp.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            tmp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tmp.GetPixels32();
            Object.Destroy(tmp);
            return px;
        }

        static Color32 Tint(Color32 c, Color32 t)
        {
            // keep the render's own shading: scale each channel by the tint, so folds and edges survive
            return new Color32((byte)(c.r * t.r / 255), (byte)(c.g * t.g / 255), (byte)(c.b * t.b / 255), c.a);
        }

        static readonly Color32 Outline = new Color32(48, 48, 64, 255);
        static readonly Color32 Gold = new Color32(240, 200, 80, 255);
        static readonly Color32 GoldDark = new Color32(190, 140, 40, 255);

        /// <summary>Hats in the sprite's own outline, over the top of the head (top-down rows 5 to 9, the head spans columns 14 to 33, facing right).</summary>
        static void DrawHat(Texture2D tex, int hat, Color32 cloth)
        {
            int h = tex.height;
            void Put(int x, int yTop, Color32 c) { if (x >= 0 && x < tex.width && yTop >= 0 && yTop < h) tex.SetPixel(x, h - 1 - yTop, c); }
            void Span(int x0, int x1, int yTop, Color32 c) { for (int x = x0; x <= x1; x++) Put(x, yTop, c); }
            var dark = new Color32((byte)(cloth.r * 0.55f), (byte)(cloth.g * 0.55f), (byte)(cloth.b * 0.55f), 255);
            var light = new Color32((byte)Mathf.Min(255, cloth.r * 0.9f + 40), (byte)Mathf.Min(255, cloth.g * 0.9f + 40), (byte)Mathf.Min(255, cloth.b * 0.9f + 40), 255);
            switch (hat)
            {
                case 1:   // a cap: a low dome and a brim forward
                    Span(15, 32, 4, Outline);
                    Span(14, 33, 5, Outline); Span(15, 32, 5, light);
                    Span(13, 34, 6, Outline); Span(14, 33, 6, light);
                    Span(13, 34, 7, Outline); Span(14, 33, 7, dark);
                    Span(13, 34, 8, Outline); Span(14, 33, 8, dark);
                    Span(24, 41, 9, Outline); Span(25, 40, 9, dark); Span(24, 41, 10, Outline);
                    break;
                case 2:   // a pointed hat with a wide brim
                    for (int row = 0; row <= 8; row++)
                    {
                        int half = 1 + row * 11 / 8;
                        Span(24 - half, 24 + half, row, Outline);
                        if (row > 0) Span(25 - half, 23 + half, row, row % 3 == 0 ? dark : light);
                    }
                    Span(10, 38, 9, Outline); Span(11, 37, 9, dark); Span(10, 38, 10, Outline);
                    break;
                case 3:   // a crown: a gold band with three points
                    Span(14, 33, 5, Outline); Span(15, 32, 5, Gold);
                    Span(14, 33, 6, Outline); Span(15, 32, 6, Gold);
                    Span(14, 33, 7, Outline); Span(15, 32, 7, GoldDark);
                    Span(14, 33, 8, Outline);
                    foreach (int cx in new[] { 16, 23, 31 })
                    {
                        Span(cx - 1, cx + 1, 3, Outline); Put(cx, 3, Gold);
                        Span(cx - 2, cx + 2, 4, Outline); Span(cx - 1, cx + 1, 4, Gold);
                        Put(cx, 2, Outline);
                    }
                    break;
            }
        }
    }
}
