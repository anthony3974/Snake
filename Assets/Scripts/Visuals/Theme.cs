using UnityEngine;

namespace NeonSnake
{
    /// <summary>Colour set for one level. The game cycles through these as you level up.</summary>
    public struct Theme
    {
        public Color bg, board, frame, head, tail;

        public Theme(string bg, string board, string frame, string head, string tail)
        {
            this.bg = Palette.Hex(bg);
            this.board = Palette.Hex(board);
            this.frame = Palette.Hex(frame);
            this.head = Palette.Hex(head);
            this.tail = Palette.Hex(tail);
        }

        public static Theme Lerp(Theme a, Theme b, float t) => new Theme
        {
            bg = Color.Lerp(a.bg, b.bg, t),
            board = Color.Lerp(a.board, b.board, t),
            frame = Color.Lerp(a.frame, b.frame, t),
            head = Color.Lerp(a.head, b.head, t),
            tail = Color.Lerp(a.tail, b.tail, t),
        };

        public static readonly Theme[] All =
        {
            new Theme("#060A16", "#101932", "#3DF5C8", "#5CFFD8", "#2A6BFF"), // mint night
            new Theme("#12071A", "#22102E", "#FF6B9A", "#FFD166", "#FF3D7F"), // sunset
            new Theme("#03111C", "#0A2236", "#4CC9F0", "#86F7FF", "#7B5CFF"), // deep ocean
            new Theme("#081207", "#132613", "#B8FF3C", "#E4FF6A", "#16C784"), // toxic lime
            new Theme("#150803", "#2A140A", "#FF8A3D", "#FFE066", "#FF3B3B"), // ember
            new Theme("#0C071E", "#1B1238", "#C77DFF", "#F7B2FF", "#5E5BFF"), // ultraviolet
        };
    }

    public static class Palette
    {
        public static readonly Color Berry = Hex("#FF4D6D");
        public static readonly Color Star = Hex("#FFD54A");
        public static readonly Color Ghost = Hex("#9FF3FF");
        public static readonly Color Clock = Hex("#6EA8FF");
        public static readonly Color Shrink = Hex("#C77DFF");
        public static readonly Color Leaf = Hex("#5BE37D");
        public static readonly Color Tongue = Hex("#FF3D6E");
        public static readonly Color Pupil = Hex("#0A0F1F");
        public static readonly Color SlowBoard = Hex("#1A2D66");

        public static Color For(FoodKind kind) => kind switch
        {
            FoodKind.Star => Star,
            FoodKind.Ghost => Ghost,
            FoodKind.Clock => Clock,
            FoodKind.Shrink => Shrink,
            _ => Berry,
        };

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
