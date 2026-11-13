using System.Collections.Generic;
using UnityEngine;

namespace NeonSnake
{
    /// <summary>Pooled sprite particles, camera shake and drifting background motes.</summary>
    public class Fx
    {
        class Particle
        {
            public Transform tr;
            public SpriteRenderer sr;
            public Vector2 pos, vel;
            public Color color;
            public float life, maxLife, size, drag, gravity, spin, angle;
            public bool grow, alive;
        }

        class Mote
        {
            public Transform tr;
            public SpriteRenderer sr;
            public Vector2 uv;
            public float speed, alpha, phase;
        }

        const int MaxParticles = 400;
        const int MoteCount = 26;

        readonly Transform root;
        readonly Camera cam;
        readonly List<Particle> particles = new List<Particle>();
        readonly List<Mote> motes = new List<Mote>();
        int cursor;
        float shake, clock;

        public Vector3 ShakeOffset { get; private set; }

        public Fx(Transform parent, Camera cam)
        {
            this.cam = cam;
            root = new GameObject("Fx").transform;
            root.SetParent(parent, false);

            for (int i = 0; i < MoteCount; i++)
            {
                var sr = NewRenderer("Mote", Art.Glow, -40);
                float size = Random.Range(0.25f, 0.7f);
                sr.transform.localScale = new Vector3(size, size, 1f);
                motes.Add(new Mote
                {
                    tr = sr.transform,
                    sr = sr,
                    uv = new Vector2(Random.value, Random.value),
                    speed = Random.Range(0.15f, 0.5f),
                    alpha = Random.Range(0.05f, 0.16f),
                    phase = Random.Range(0f, 10f),
                });
            }
        }

        SpriteRenderer NewRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        Particle Next()
        {
            for (int k = 0; k < particles.Count; k++)
            {
                cursor = (cursor + 1) % particles.Count;
                if (!particles[cursor].alive) return particles[cursor];
            }
            if (particles.Count < MaxParticles)
            {
                var sr = NewRenderer("Particle", Art.Glow, 2000);
                var p = new Particle { tr = sr.transform, sr = sr };
                particles.Add(p);
                return p;
            }
            cursor = (cursor + 1) % particles.Count;
            return particles[cursor];
        }

        void Emit(Vector2 at, Vector2 vel, Color color, float size, float life, Sprite sprite, float gravity, float spin, bool grow)
        {
            var p = Next();
            p.pos = at;
            p.vel = vel;
            p.color = color;
            p.size = size;
            p.life = p.maxLife = life;
            p.gravity = gravity;
            p.drag = grow ? 0f : 3.5f;
            p.spin = spin;
            p.angle = Random.Range(0f, 360f);
            p.grow = grow;
            p.alive = true;
            p.sr.sprite = sprite;
            p.sr.enabled = true;
            p.tr.localPosition = at;
            p.tr.localScale = Vector3.zero;
        }

        public void Burst(Vector2 at, Color color, int count, float speed, float size = 0.3f, float life = 0.55f, float gravity = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir == Vector2.zero) dir = Vector2.up;
                Emit(at, dir * speed * Random.Range(0.3f, 1f), color, size * Random.Range(0.6f, 1.3f),
                    life * Random.Range(0.7f, 1.2f), Art.Glow, gravity, 0f, false);
            }
        }

        /// <summary>Expanding shockwave ring.</summary>
        public void Ring(Vector2 at, Color color, float size = 0.7f, float life = 0.4f)
            => Emit(at, Vector2.zero, color, size, life, Art.Ring, 0f, 0f, true);

        public void Confetti(Vector2 at, Color[] colors, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var vel = new Vector2(Random.Range(-6f, 6f), Random.Range(5f, 13f));
                Emit(at, vel, colors[Random.Range(0, colors.Length)], Random.Range(0.14f, 0.24f),
                    Random.Range(1.4f, 2.2f), Art.Pixel, 11f, Random.Range(-540f, 540f), false);
            }
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        public void Tick(float dt, Color moteColor)
        {
            clock += dt;

            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                if (!p.alive) continue;
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.alive = false;
                    p.sr.enabled = false;
                    continue;
                }
                p.vel *= 1f / (1f + p.drag * dt);
                p.vel.y -= p.gravity * dt;
                p.pos += p.vel * dt;
                p.angle += p.spin * dt;
                float k = p.life / p.maxLife;
                float s = p.grow ? p.size * (1f + 1.8f * (1f - k)) : p.size * (0.35f + 0.65f * k);
                p.tr.localPosition = p.pos;
                p.tr.localScale = new Vector3(s, s, 1f);
                p.tr.localRotation = Quaternion.Euler(0f, 0f, p.angle);
                p.sr.color = p.color.WithAlpha(p.color.a * (p.grow ? k : Mathf.Min(1f, k * 1.5f)));
            }

            if (shake > 0.0001f)
            {
                float nx = Mathf.PerlinNoise(clock * 28f, 0.3f) * 2f - 1f;
                float ny = Mathf.PerlinNoise(0.7f, clock * 28f) * 2f - 1f;
                ShakeOffset = new Vector3(nx, ny, 0f) * shake;
                shake = Mathf.MoveTowards(shake, 0f, dt * (0.6f + shake * 3f));
            }
            else ShakeOffset = Vector3.zero;

            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector2 center = cam.transform.position;
            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                m.uv.y += m.speed * dt / (2f * halfH);
                m.uv.x += Mathf.Sin(clock * 0.5f + m.phase) * 0.004f * dt;
                if (m.uv.y > 1.05f)
                {
                    m.uv.y = -0.05f;
                    m.uv.x = Random.value;
                }
                m.tr.localPosition = new Vector3(center.x + (m.uv.x - 0.5f) * 2f * halfW, center.y + (m.uv.y - 0.5f) * 2f * halfH, 0f);
                m.sr.color = moteColor.WithAlpha(m.alpha * (0.6f + 0.4f * Mathf.Sin(clock * 1.3f + m.phase)));
            }
        }
    }
}
