using System.Collections.Generic;
using UnityEngine;

namespace NeonSnake
{
    /// <summary>Animated pickup. Timed pickups get a shrinking ring and blink before they vanish.</summary>
    public class FoodView
    {
        readonly GameObject go;
        readonly Transform root, body, spinner;
        readonly SpriteRenderer glow, ring;
        readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
        readonly List<float> baseAlpha = new List<float>();
        readonly FoodKind kind;
        readonly Color color;
        float age, shownVisibility = 1f;

        public FoodView(FoodKind kind, Transform parent, Vector2 position)
        {
            this.kind = kind;
            color = Palette.For(kind);
            go = new GameObject(kind.ToString());
            root = go.transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localScale = Vector3.zero;
            body = new GameObject("Body").transform;
            body.SetParent(root, false);

            glow = Add(root, Art.Glow, color, 0.5f, 40, Vector2.zero, Vector2.one * 1.9f, false);
            if (kind != FoodKind.Berry)
                ring = Add(root, Art.Ring, color, 0.55f, 49, Vector2.zero, Vector2.one, true);

            switch (kind)
            {
                case FoodKind.Berry:
                    Add(body, Art.Circle, color, 1f, 50, Vector2.zero, Vector2.one * 0.64f, true);
                    var leaf = Add(body, Art.Circle, Palette.Leaf, 1f, 51, new Vector2(0.12f, 0.3f), new Vector2(0.26f, 0.13f), true);
                    leaf.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
                    Add(body, Art.Circle, Color.white, 0.7f, 52, new Vector2(-0.12f, 0.1f), Vector2.one * 0.15f, true);
                    break;
                case FoodKind.Star:
                    spinner = Add(body, Art.Star, color, 1f, 50, Vector2.zero, Vector2.one * 0.86f, true).transform;
                    break;
                case FoodKind.Ghost:
                    Add(body, Art.Diamond, color, 0.9f, 50, Vector2.zero, Vector2.one * 0.74f, true);
                    Add(body, Art.Diamond, Color.white, 0.85f, 51, Vector2.zero, Vector2.one * 0.32f, true);
                    break;
                case FoodKind.Clock:
                    Add(body, Art.Ring, color, 1f, 50, Vector2.zero, Vector2.one * 0.72f, true);
                    spinner = new GameObject("Hand").transform;
                    spinner.SetParent(body, false);
                    Add(spinner, Art.Pixel, color, 1f, 51, new Vector2(0f, 0.12f), new Vector2(0.07f, 0.24f), true);
                    Add(body, Art.Circle, color, 1f, 52, Vector2.zero, Vector2.one * 0.13f, true);
                    break;
                case FoodKind.Shrink:
                    Add(body, Art.Triangle, color, 1f, 50, Vector2.zero, Vector2.one * 0.74f, true);
                    Add(body, Art.Triangle, Color.white, 0.6f, 51, new Vector2(0f, 0.03f), Vector2.one * 0.3f, true);
                    break;
            }
        }

        SpriteRenderer Add(Transform parent, Sprite sprite, Color c, float alpha, int order, Vector2 pos, Vector2 scale, bool track)
        {
            var t = new GameObject("Part").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = c.WithAlpha(alpha);
            if (track)
            {
                parts.Add(sr);
                baseAlpha.Add(alpha);
            }
            return sr;
        }

        /// <param name="lifeFraction">Remaining life 0..1, or negative for food that never expires.</param>
        public void Tick(float dt, float lifeFraction)
        {
            age += dt;
            float pop = Ease.OutBack(Mathf.Clamp01(age / 0.35f));
            float pulse = 1f + 0.07f * Mathf.Sin(age * 5.5f);
            root.localScale = Vector3.one * (pop * pulse);

            switch (kind)
            {
                case FoodKind.Star: spinner.localRotation = Quaternion.Euler(0f, 0f, -age * 70f); break;
                case FoodKind.Clock: spinner.localRotation = Quaternion.Euler(0f, 0f, -age * 300f); break;
                case FoodKind.Ghost: body.localPosition = new Vector3(0f, 0.05f * Mathf.Sin(age * 4f), 0f); break;
            }

            float visibility = 1f;
            if (lifeFraction >= 0f && ring != null)
            {
                ring.transform.localScale = Vector3.one * (0.92f + 0.7f * lifeFraction);
                if (lifeFraction < 0.3f) visibility = Mathf.Sin(age * 26f) > -0.2f ? 1f : 0.25f;
            }
            if (!Mathf.Approximately(visibility, shownVisibility))
            {
                shownVisibility = visibility;
                for (int i = 0; i < parts.Count; i++)
                    parts[i].color = parts[i].color.WithAlpha(baseAlpha[i] * visibility);
            }
            glow.color = color.WithAlpha((0.42f + 0.12f * Mathf.Sin(age * 5.5f)) * visibility);
        }

        public void Destroy() => Object.Destroy(go);
    }

    /// <summary>Obstacle block that appears from level 3 onwards.</summary>
    public class BlockView
    {
        readonly GameObject go;
        readonly Transform root;
        readonly SpriteRenderer fill, edge, shine;
        float age;

        public BlockView(Transform parent, Vector2 position)
        {
            go = new GameObject("Block");
            root = go.transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localScale = Vector3.zero;

            fill = Make(Art.Block, 40);
            fill.transform.localScale = new Vector3(0.86f, 0.86f, 1f);
            edge = Make(Art.Frame, 41);
            edge.drawMode = SpriteDrawMode.Sliced;
            edge.size = new Vector2(0.9f, 0.9f);
            shine = Make(Art.Glow, 42);
            shine.transform.localPosition = new Vector3(-0.15f, 0.15f, 0f);
            shine.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            shine.color = new Color(1f, 1f, 1f, 0.14f);
        }

        SpriteRenderer Make(Sprite sprite, int order)
        {
            var t = new GameObject("Part").transform;
            t.SetParent(root, false);
            var sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        public void Tick(float dt, in Theme theme)
        {
            age += dt;
            root.localScale = Vector3.one * Ease.OutBack(Mathf.Clamp01(age / 0.45f));
            fill.color = Color.Lerp(theme.board, theme.frame, 0.25f);
            edge.color = theme.frame.WithAlpha(0.65f + 0.35f * Mathf.Sin(age * 3f));
        }

        public void Destroy() => Object.Destroy(go);
    }
}
