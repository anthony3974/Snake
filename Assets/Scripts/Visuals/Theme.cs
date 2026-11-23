using UnityEngine;

namespace Snake
{
    /// <summary>Natural matte colour set for one level. The game cycles through these as you level up.</summary>
    public struct Theme
    {
        public Color bg, board, frame, head, tail, accent;

        public Theme(string bg, string board, string frame, string head, string tail, string accent)
        {
            this.bg = Palette.Hex(bg);
            this.board = Palette.Hex(board);
            this.frame = Palette.Hex(frame);
            this.head = Palette.Hex(head);
            this.tail = Palette.Hex(tail);
            this.accent = Palette.Hex(accent);
        }

        public static Theme Lerp(Theme a, Theme b, float t) => new Theme
        {
            bg = Color.Lerp(a.bg, b.bg, t),
            board = Color.Lerp(a.board, b.board, t),
            frame = Color.Lerp(a.frame, b.frame, t),
            head = Color.Lerp(a.head, b.head, t),
            tail = Color.Lerp(a.tail, b.tail, t),
            accent = Color.Lerp(a.accent, b.accent, t),
        };

        public static readonly Theme[] All =
        {
            new Theme("#4A752C", "#AAD751", "#578A34", "#4674E9", "#355BC2", "#E8F5E1"), // meadow
            new Theme("#2B4C59", "#9AC2C9", "#3B6575", "#3E569E", "#2C407D", "#E2F1F4"), // coastal
            new Theme("#6E563B", "#DFC9A6", "#8A6D4D", "#2E7D5B", "#205E43", "#F7EFE2"), // sand
            new Theme("#233B2E", "#6E9C7B", "#325442", "#E6A832", "#C4821B", "#E3F2E8"), // pine
            new Theme("#2D3247", "#8E98B5", "#414866", "#2B8A9E", "#1E6878", "#E8EBF5"), // slate
            new Theme("#1F2428", "#2E353B", "#444E57", "#5CB85C", "#3E8E41", "#E6EDF3"), // charcoal
        };
    }

    public static class Palette
    {
        public static readonly Color Berry = Hex("#E53935");
        public static readonly Color Leaf = Hex("#43A047");
        public static readonly Color Gold = Hex("#F5C542");
        public static readonly Color Tongue = Hex("#E53935");
        public static readonly Color Pupil = Hex("#141920");
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.18f);

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    public static class Ease
    {
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static float OutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
