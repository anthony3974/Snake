using UnityEngine;

namespace Snake
{
    /// <summary>Animated berry pickup.</summary>
    public class FoodView
    {
        readonly GameObject go;
        readonly Transform root;
        float age;

        public FoodView(Transform parent, Vector2 position)
        {
            go = new GameObject("Berry");
            root = go.transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localScale = Vector3.zero;

            Add(root, Art.Circle, Palette.Shadow, 49, new Vector2(0f, -0.06f), Vector2.one * 0.66f);
            Add(root, Art.Circle, Palette.Berry, 50, Vector2.zero, Vector2.one * 0.66f);
            var leaf = Add(root, Art.Circle, Palette.Leaf, 51, new Vector2(0.13f, 0.30f), new Vector2(0.26f, 0.13f));
            leaf.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
            Add(root, Art.Circle, Color.white.WithAlpha(0.45f), 52, new Vector2(-0.13f, 0.12f), Vector2.one * 0.14f);
        }

        static SpriteRenderer Add(Transform parent, Sprite sprite, Color c, int order, Vector2 pos, Vector2 scale)
        {
            var t = new GameObject("Part").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = c;
            return sr;
        }

        public void Tick(float dt)
        {
            age += dt;
            float pop = Ease.OutBack(Mathf.Clamp01(age / 0.3f));
            float pulse = 1f + 0.04f * Mathf.Sin(age * 4.5f);
            root.localScale = Vector3.one * (pop * pulse);
        }

        public void Destroy() => Object.Destroy(go);
    }

    /// <summary>Obstacle block that appears from level 3 onwards.</summary>
    public class BlockView
    {
        readonly GameObject go;
        readonly Transform root;
        readonly SpriteRenderer fill, edge;
        float age;

        public BlockView(Transform parent, Vector2 position)
        {
            go = new GameObject("Block");
            root = go.transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localScale = Vector3.zero;

            var shadow = Make(Art.Block, 39);
            shadow.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            shadow.transform.localScale = new Vector3(0.86f, 0.86f, 1f);
            shadow.color = Palette.Shadow;

            fill = Make(Art.Block, 40);
            fill.transform.localScale = new Vector3(0.86f, 0.86f, 1f);
            edge = Make(Art.Frame, 41);
            edge.drawMode = SpriteDrawMode.Sliced;
            edge.size = new Vector2(0.88f, 0.88f);
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
            root.localScale = Vector3.one * Ease.OutBack(Mathf.Clamp01(age / 0.4f));
            fill.color = theme.block;
            edge.color = Color.Lerp(theme.block, Color.black, 0.25f);
        }

        public void Destroy() => Object.Destroy(go);
    }
}
