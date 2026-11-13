using System;
using UnityEngine;

namespace NeonSnake
{
    /// <summary>
    /// Every sprite in the game is generated here at runtime, so the project needs no imported art.
    /// Shapes are white with an alpha mask and get tinted through SpriteRenderer/Image colour.
    /// </summary>
    public static class Art
    {
        static Sprite circle, glow, ring, star, diamond, triangle, block, frame, frameGlow, pixel, uiPanel;

        /// <summary>Filled circle, about 1 world unit across.</summary>
        public static Sprite Circle => Get(ref circle, () => Shape(128, 128f, (x, y) => Fill(Len(x, y) - 61f)));

        /// <summary>Soft radial glow that fades to nothing at the edge.</summary>
        public static Sprite Glow => Get(ref glow, () => Shape(128, 128f, (x, y) =>
        {
            float d = Mathf.Clamp01(1f - Len(x, y) / 63f);
            return d * d;
        }));

        public static Sprite Ring => Get(ref ring, () => Shape(128, 128f, (x, y) => Fill(Mathf.Abs(Len(x, y) - 54f) - 6f)));

        public static Sprite Diamond => Get(ref diamond, () => Shape(128, 128f, (x, y) => Fill((Mathf.Abs(x) + Mathf.Abs(y)) * 0.7071f - 42f)));

        public static Sprite Star => Get(ref star, () => Poly(128, 128f, StarPoints(61f, 25f)));

        /// <summary>Triangle pointing down.</summary>
        public static Sprite Triangle => Get(ref triangle, () => Poly(128, 128f, new[]
        {
            new Vector2(0f, -56f), new Vector2(56f, 38f), new Vector2(-56f, 38f),
        }));

        /// <summary>Rounded square used for obstacles.</summary>
        public static Sprite Block => Get(ref block, () => Shape(64, 64f, (x, y) => Fill(RoundBox(x, y, 31f, 31f, 13f))));

        /// <summary>Thin rounded outline, 9-sliced so it can wrap the board at any size.</summary>
        public static Sprite Frame => Get(ref frame, () => Shape(64, 64f,
            (x, y) => Fill(Mathf.Abs(RoundBox(x, y, 30f, 30f, 16f)) - 1.75f),
            new Vector4(24f, 24f, 24f, 24f)));

        /// <summary>Soft glow around a rounded rectangle, 9-sliced.</summary>
        public static Sprite FrameGlow => Get(ref frameGlow, () => Shape(128, 64f, (x, y) =>
        {
            float d = RoundBox(x, y, 40f, 40f, 20f);
            float a = Mathf.Exp(-(d * d) / (2f * 9f * 9f));
            return d < 0f ? a * 0.6f : a;
        }, new Vector4(48f, 48f, 48f, 48f)));

        /// <summary>Solid white square, 1 world unit.</summary>
        public static Sprite Pixel => Get(ref pixel, () => Shape(4, 4f, (x, y) => 1f, default, FilterMode.Point));

        /// <summary>Rounded panel for uGUI buttons (9-sliced).</summary>
        public static Sprite UIPanel => Get(ref uiPanel, () => Shape(96, 100f,
            (x, y) => Fill(RoundBox(x, y, 47f, 47f, 32f)),
            new Vector4(40f, 40f, 40f, 40f)));

        /// <summary>One pixel per board cell, alternating shades, tinted by the theme.</summary>
        public static Texture2D Checker(int cols, int rows)
        {
            var tex = new Texture2D(cols, rows, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Checker",
            };
            var px = new Color32[cols * rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    byte v = ((x + y) & 1) == 0 ? (byte)255 : (byte)218;
                    px[y * cols + x] = new Color32(v, v, v, 255);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Sprite Get(ref Sprite cached, Func<Sprite> make)
        {
            // Unity's == also catches sprites destroyed when leaving play mode.
            if (cached == null) cached = make();
            return cached;
        }

        static Sprite Shape(int size, float ppu, Func<float, float, float> alpha, Vector4 border = default,
            FilterMode filter = FilterMode.Bilinear)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = filter,
                name = "Generated",
            };
            var px = new Color32[size * size];
            float h = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(alpha(x + 0.5f - h, y + 0.5f - h));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu, 0,
                SpriteMeshType.FullRect, border);
        }

        static Sprite Poly(int size, float ppu, Vector2[] points) => Shape(size, ppu, (x, y) => Coverage(points, x, y));

        static float Fill(float signedDistance) => Mathf.Clamp01(0.5f - signedDistance);

        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float RoundBox(float x, float y, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x) - hx + r, qy = Mathf.Abs(y) - hy + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Vector2[] StarPoints(float outer, float inner)
        {
            var p = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = (90f + i * 36f) * Mathf.Deg2Rad;
                float r = (i & 1) == 0 ? outer : inner;
                p[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return p;
        }

        // 4x4 supersampled point-in-polygon for smooth edges.
        static float Coverage(Vector2[] poly, float x, float y)
        {
            const int n = 4;
            int hit = 0;
            for (int sy = 0; sy < n; sy++)
                for (int sx = 0; sx < n; sx++)
                    if (Inside(poly, x + (sx + 0.5f) / n - 0.5f, y + (sy + 0.5f) / n - 0.5f))
                        hit++;
            return hit / (float)(n * n);
        }

        static bool Inside(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if ((p[i].y > y) != (p[j].y > y) &&
                    x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
