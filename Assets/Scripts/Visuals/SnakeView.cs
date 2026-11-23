using System.Collections.Generic;
using UnityEngine;

namespace Snake
{
    /// <summary>
    /// Draws the snake as a smooth solid tube. Segments slide between grid cells every frame
    /// (interpolated), so movement looks fluid even though the rules run on a grid tick.
    /// The head has eyes that blink and track the berry, plus a flicking tongue.
    /// </summary>
    public class SnakeView
    {
        public struct Look
        {
            public Color head, tail;
            public float angle, time, breathe;
            public bool hasLookAt;
            public Vector2 lookAt;
            public int hidden; // segments popped from the head end while dying
        }

        const int HeadOrder = 1100, BodyOrder = 1000;
        static readonly Vector2 EyeOffset = new Vector2(0.15f, 0.21f);

        readonly Transform root, head;
        readonly SpriteRenderer headSr, eyeL, eyeR, pupilL, pupilR, tongue;
        readonly List<SpriteRenderer> segs = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> fills = new List<SpriteRenderer>();
        Vector2[] pos = new Vector2[64];
        float blinkIn = 2f, blinkT = -1f, tongueIn = 2.5f, tongueT = -1f, chompT = -1f;
        bool dead;

        public Vector2 HeadPosition { get; private set; }

        public SnakeView(Transform parent)
        {
            root = new GameObject("Snake").transform;
            root.SetParent(parent, false);
            head = new GameObject("Head").transform;
            head.SetParent(root, false);

            headSr = Make("HeadBody", head, Art.Circle, HeadOrder);
            tongue = Make("Tongue", head, Art.Pixel, HeadOrder - 1);
            eyeL = Make("EyeL", head, Art.Circle, HeadOrder + 1);
            eyeR = Make("EyeR", head, Art.Circle, HeadOrder + 1);
            pupilL = Make("PupilL", head, Art.Circle, HeadOrder + 2);
            pupilR = Make("PupilR", head, Art.Circle, HeadOrder + 2);
        }

        public void Chomp() => chompT = 0f;
        public void SetDead() => dead = true;

        public void Revive()
        {
            dead = false;
            chompT = tongueT = blinkT = -1f;
        }

        public void Render(List<Vector2Int> body, List<Vector2Int> prev, float t, in Look look, List<float> bulges, float dt)
        {
            int n = body.Count;
            EnsureCapacity(n);
            var half = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < n; i++)
                pos[i] = Vector2.LerpUnclamped(prev[i], body[i], t) + half;

            for (int i = 0; i < segs.Count; i++)
            {
                bool on = i < n && i >= look.hidden;
                SpriteRenderer seg = segs[i], fill = fills[i];
                bool fillOn = on && i < n - 1;
                seg.enabled = on && i > 0; // index 0 is drawn by the head
                fill.enabled = fillOn;
                if (!on) continue;

                float w = Width(i, n) * Swell(i, bulges, look);
                Color c = ColorAt(i, n, look);

                if (i > 0)
                {
                    Put(seg, pos[i], w);
                    seg.color = c;
                }
                if (fillOn)
                {
                    // Extra circle between neighbours so the body reads as one continuous tube.
                    float w2 = 0.5f * (w + Width(i + 1, n) * Swell(i + 1, bulges, look));
                    Put(fill, (pos[i] + pos[i + 1]) * 0.5f, w2);
                    fill.color = Color.Lerp(c, ColorAt(i + 1, n, look), 0.5f);
                }
            }

            RenderHead(n > 0 && look.hidden == 0, look, bulges, dt);
        }

        void RenderHead(bool on, in Look look, List<float> bulges, float dt)
        {
            if (head.gameObject.activeSelf != on) head.gameObject.SetActive(on);
            if (!on) return;

            Vector2 p = pos[0];
            HeadPosition = p;
            head.localPosition = p;
            head.localRotation = Quaternion.Euler(0f, 0f, look.angle);

            float sx = 1f, sy = 1f;
            if (chompT >= 0f)
            {
                chompT += dt;
                float u = chompT / 0.2f;
                if (u >= 1f) chompT = -1f;
                else
                {
                    float k = Mathf.Sin(u * Mathf.PI);
                    sx += 0.14f * k;
                    sy += 0.14f * k;
                }
            }
            float s = 0.95f * Swell(0, bulges, look);
            head.localScale = new Vector3(s * sx, s * sy, 1f);
            headSr.color = ColorAt(0, 1, look);

            // Eyes: blink now and then, pupils follow the berry.
            float open = 1f;
            if (dead) open = 0f;
            else
            {
                blinkIn -= dt;
                if (blinkIn <= 0f)
                {
                    blinkIn = Random.Range(1.8f, 4.5f);
                    blinkT = 0f;
                }
                if (blinkT >= 0f)
                {
                    blinkT += dt;
                    float u = blinkT / 0.16f;
                    if (u >= 1f) blinkT = -1f;
                    else open = 1f - Mathf.Sin(u * Mathf.PI);
                }
            }

            Vector2 lookDir = Vector2.right;
            if (look.hasLookAt && !dead)
            {
                Vector2 toFood = look.lookAt - p;
                if (toFood.sqrMagnitude > 0.01f)
                    lookDir = Quaternion.Euler(0f, 0f, -look.angle) * toFood.normalized;
            }
            Eye(eyeL, pupilL, new Vector2(EyeOffset.x, EyeOffset.y), open, lookDir);
            Eye(eyeR, pupilR, new Vector2(EyeOffset.x, -EyeOffset.y), open, lookDir);

            // Tongue flick.
            float e = 0f;
            if (!dead)
            {
                tongueIn -= dt;
                if (tongueIn <= 0f)
                {
                    tongueIn = Random.Range(2f, 5f);
                    tongueT = 0f;
                }
                if (tongueT >= 0f)
                {
                    tongueT += dt;
                    float u = tongueT / 0.32f;
                    if (u >= 1f) tongueT = -1f;
                    else e = Mathf.Sin(u * Mathf.PI);
                }
            }
            tongue.enabled = e > 0.02f;
            if (tongue.enabled)
            {
                tongue.transform.localPosition = new Vector3(0.4f + 0.17f * e, 0f, 0f);
                tongue.transform.localScale = new Vector3(0.36f * e, 0.07f, 1f);
                tongue.color = Palette.Tongue;
            }
        }

        void Eye(SpriteRenderer eye, SpriteRenderer pupil, Vector2 at, float open, Vector2 lookDir)
        {
            eye.transform.localPosition = at;
            eye.transform.localScale = new Vector3(0.32f, 0.32f * Mathf.Max(0.12f, open), 1f);
            eye.color = dead ? new Color(0.12f, 0.14f, 0.16f, 1f) : Color.white;
            pupil.enabled = open > 0.45f;
            if (!pupil.enabled) return;
            pupil.transform.localPosition = at + lookDir * 0.065f;
            pupil.transform.localScale = new Vector3(0.16f, 0.16f, 1f);
            pupil.color = Palette.Pupil;
        }

        void EnsureCapacity(int n)
        {
            if (pos.Length < n) pos = new Vector2[Mathf.NextPowerOfTwo(n)];
            while (segs.Count < n)
            {
                int i = segs.Count;
                segs.Add(Make("Seg", root, Art.Circle, BodyOrder - 2 * i));
                fills.Add(Make("Fill", root, Art.Circle, BodyOrder - 2 * i - 1));
            }
        }

        static SpriteRenderer Make(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static void Put(SpriteRenderer sr, Vector2 p, float scale)
        {
            var t = sr.transform;
            t.localPosition = p;
            t.localScale = new Vector3(scale, scale, 1f);
        }

        static float Width(int i, int n)
        {
            float f = n > 1 ? i / (float)(n - 1) : 0f;
            return f < 0.55f ? 0.8f : Mathf.Lerp(0.8f, 0.45f, (f - 0.55f) / 0.45f);
        }

        // Swallowed-food bulges travelling down the body, plus idle breathing on the ready screen.
        static float Swell(int i, List<float> bulges, in Look look)
        {
            float s = 1f;
            for (int b = 0; b < bulges.Count; b++)
            {
                float d = i - bulges[b];
                s += 0.3f * Mathf.Exp(-d * d * 0.8f);
            }
            if (look.breathe > 0f)
                s *= 1f + 0.05f * look.breathe * Mathf.Sin(look.time * 3.2f - i * 0.6f);
            return s;
        }

        static Color ColorAt(int i, int n, in Look look)
        {
            float f = n > 1 ? i / (float)(n - 1) : 0f;
            Color c = Color.Lerp(look.head, look.tail, f);
            c.a = 1f;
            return c;
        }
    }
}
