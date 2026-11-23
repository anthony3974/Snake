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
            public float angle, time, breathe, press;
            public bool hasLookAt;
            public Vector2 lookAt;
            public int hidden; // segments popped from the head end while dying
        }

        const int HeadOrder = 1100, BodyOrder = 1000, BodyMeshOrder = 200;
        static readonly Vector2 EyeOffset = new Vector2(0.15f, 0.21f);

        readonly Transform root, head;
        readonly SpriteRenderer headSr, eyeL, eyeR, pupilL, pupilR, tongue;
        readonly Mesh bodyMesh;
        readonly MeshRenderer bodyMr;
        readonly List<SpriteRenderer> segs = new List<SpriteRenderer>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> vertColors = new List<Color>();
        readonly List<int> tris = new List<int>();
        Vector2[] pos = new Vector2[64];
        float[] widths = new float[64];
        Color[] segColors = new Color[64];
        float blinkIn = 2f, blinkT = -1f, tongueIn = 2.5f, tongueT = -1f, chompT = -1f;
        bool dead;

        public Vector2 HeadPosition { get; private set; }

        public SnakeView(Transform parent)
        {
            root = new GameObject("Snake").transform;
            root.SetParent(parent, false);
            head = new GameObject("Head").transform;
            head.SetParent(root, false);

            var bodyGo = new GameObject("BodyMesh");
            bodyGo.transform.SetParent(root, false);
            bodyMesh = new Mesh { name = "SnakeBody" };
            bodyMesh.MarkDynamic();
            bodyGo.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            bodyMr = bodyGo.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = Art.TubeTex };
            bodyMr.sharedMaterial = mat;
            bodyMr.sortingOrder = BodyMeshOrder;

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
            {
                pos[i] = Vector2.LerpUnclamped(prev[i], body[i], t) + half;
                widths[i] = Width(i, n) * Swell(i, bulges, look);
                segColors[i] = ColorAt(i, n, look);
            }

            for (int i = 0; i < segs.Count; i++)
            {
                bool on = i < n && i >= look.hidden && i > 0; // index 0 is drawn by the head
                SpriteRenderer seg = segs[i];
                seg.enabled = on;
                if (!on) continue;

                Put(seg, pos[i], widths[i]);
                seg.color = segColors[i];
            }

            BuildBodyMesh(n, look.hidden);
            RenderHead(n > 0 && look.hidden == 0, look, bulges, dt);
        }

        void BuildBodyMesh(int n, int hidden)
        {
            verts.Clear();
            uvs.Clear();
            vertColors.Clear();
            tris.Clear();

            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            int first = Mathf.Max(0, hidden);
            for (int i = first; i < n - 1; i++)
            {
                Vector2 p0 = pos[i], p1 = pos[i + 1];
                float w0 = widths[i], w1 = widths[i + 1];
                float r0 = w0 * Art.CircleRadius, r1 = w1 * Art.CircleRadius;
                Vector2 delta = p1 - p0;
                float d = delta.magnitude;
                if (d <= Mathf.Abs(r0 - r1) + 1e-4f) continue;

                Vector2 dir = delta / d;
                Vector2 perp = new Vector2(-dir.y, dir.x);
                float cosA = (r0 - r1) / d;
                float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));

                // Exact outer-tangent chord midpoints and feathered quad half-widths so the
                // visible alpha=0.5 contour sits strictly tangent to both joint circles.
                Vector2 m0 = p0 + dir * (r0 * cosA);
                Vector2 m1 = p1 + dir * (r1 * cosA);
                float h0 = 0.5f * w0 * sinA;
                float h1 = 0.5f * w1 * sinA;

                Vector2 v0R = m0 - perp * h0;
                Vector2 v0L = m0 + perp * h0;
                Vector2 v1L = m1 + perp * h1;
                Vector2 v1R = m1 - perp * h1;

                Color c0 = linear ? segColors[i].linear : segColors[i];
                Color c1 = linear ? segColors[i + 1].linear : segColors[i + 1];

                int b = verts.Count;
                verts.Add(v0R);
                verts.Add(v0L);
                verts.Add(v1L);
                verts.Add(v1R);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));
                vertColors.Add(c0);
                vertColors.Add(c0);
                vertColors.Add(c1);
                vertColors.Add(c1);
                tris.Add(b);
                tris.Add(b + 1);
                tris.Add(b + 2);
                tris.Add(b);
                tris.Add(b + 2);
                tris.Add(b + 3);
            }

            bodyMesh.Clear();
            if (verts.Count == 0)
            {
                bodyMr.enabled = false;
                return;
            }
            bodyMr.enabled = true;
            bodyMesh.SetVertices(verts);
            bodyMesh.SetUVs(0, uvs);
            bodyMesh.SetColors(vertColors);
            bodyMesh.SetTriangles(tris, 0);
            bodyMesh.RecalculateBounds();
        }

        void RenderHead(bool on, in Look look, List<float> bulges, float dt)
        {
            if (head.gameObject.activeSelf != on) head.gameObject.SetActive(on);
            if (!on) return;

            Vector2 p = pos[0];
            HeadPosition = p;
            float press = look.press > 0f ? Ease.OutCubic(Mathf.Clamp01(look.press)) : 0f;
            float rad = look.angle * Mathf.Deg2Rad;
            Vector2 nudge = press > 0f ? new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (0.045f * press) : Vector2.zero;
            head.localPosition = p + nudge;
            head.localRotation = Quaternion.Euler(0f, 0f, look.angle);

            float sx = 1f - 0.10f * press, sy = 1f + 0.08f * press;
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
            if (pos.Length < n)
            {
                int cap = Mathf.NextPowerOfTwo(n);
                pos = new Vector2[cap];
                widths = new float[cap];
                segColors = new Color[cap];
            }
            while (segs.Count < n)
            {
                int i = segs.Count;
                segs.Add(Make("Seg", root, Art.Circle, BodyOrder - i));
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
