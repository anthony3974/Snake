using System;
using UnityEngine;

namespace Snake
{
    /// <summary>
    /// Every sprite in the game is generated here at runtime, so the project needs no imported art.
    /// Shapes are white with an alpha mask and get tinted through SpriteRenderer/Image colour.
    /// </summary>
    public static class Art
    {
        /// <summary>Visible circle radius in world units at scale 1 (matches the 61px radius in the 128px texture).</summary>
        public const float CircleRadius = 61f / 128f;

        static Sprite circle, ring, frame, pixel, uiPanel;
        static Texture2D tubeTex;

        /// <summary>Filled circle, about 1 world unit across.</summary>
        public static Sprite Circle => Get(ref circle, () => Shape(128, 128f, (x, y) => Fill(Len(x, y) - 61f)));

        /// <summary>
        /// 1D vertically feathered alpha strip matching Circle's anti-aliased edge, used for smooth
        /// tangent-matched body segments between joints.
        /// </summary>
        public static Texture2D TubeTex
        {
            get
            {
                if (tubeTex == null) tubeTex = MakeTubeTex();
                return tubeTex;
            }
        }

        public static Sprite Ring => Get(ref ring, () => Shape(128, 128f, (x, y) => Fill(Mathf.Abs(Len(x, y) - 54f) - 5f)));

        /// <summary>Clean rounded outline, 9-sliced so it can wrap the board at any size.</summary>
        public static Sprite Frame => Get(ref frame, () => Shape(64, 64f,
            (x, y) => Fill(Mathf.Abs(RoundBox(x, y, 29.5f, 29.5f, 15f)) - 2.2f),
            new Vector4(24f, 24f, 24f, 24f)));

        /// <summary>Solid white square, 1 world unit.</summary>
        public static Sprite Pixel => Get(ref pixel, () => Shape(4, 4f, (x, y) => 1f, default, FilterMode.Point));

        /// <summary>Rounded panel for uGUI buttons (9-sliced).</summary>
        public static Sprite UIPanel => Get(ref uiPanel, () => Shape(96, 100f,
            (x, y) => Fill(RoundBox(x, y, 47f, 47f, 28f)),
            new Vector4(38f, 38f, 38f, 38f)));

        /// <summary>One pixel per board cell, alternating subtle shades, tinted by the theme.</summary>
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
                    byte v = ((x + y) & 1) == 0 ? (byte)255 : (byte)240;
                    px[y * cols + x] = new Color32(v, v, v, 255);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D MakeTubeTex()
        {
            const int w = 4, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "TubeTex",
            };
            var px = new Color32[w * h];
            float half = h * 0.5f;
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Clamp01(Fill(Mathf.Abs(y + 0.5f - half) - 61f));
                var c = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                for (int x = 0; x < w; x++) px[y * w + x] = c;
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

        static float Fill(float signedDistance) => Mathf.Clamp01(0.5f - signedDistance);

        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float RoundBox(float x, float y, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x) - hx + r, qy = Mathf.Abs(y) - hy + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }
}
