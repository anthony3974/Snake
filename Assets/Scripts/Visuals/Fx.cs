using System.Collections.Generic;
using UnityEngine;

namespace Snake
{
    /// <summary>Pooled sprite particles and camera shake.</summary>
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

        const int MaxParticles = 300;

        readonly Transform root;
        readonly List<Particle> particles = new List<Particle>();
        int cursor;
        float shake, clock;

        public Vector3 ShakeOffset { get; private set; }

        public Fx(Transform parent)
        {
            root = new GameObject("Fx").transform;
            root.SetParent(parent, false);
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
                var sr = NewRenderer("Particle", Art.Circle, 2000);
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

        public void Burst(Vector2 at, Color color, int count, float speed, float size = 0.22f, float life = 0.45f, float gravity = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir == Vector2.zero) dir = Vector2.up;
                Emit(at, dir * speed * Random.Range(0.3f, 1f), color, size * Random.Range(0.6f, 1.25f),
                    life * Random.Range(0.7f, 1.15f), Art.Circle, gravity, 0f, false);
            }
        }

        /// <summary>Expanding feedback ring.</summary>
        public void Ring(Vector2 at, Color color, float size = 0.65f, float life = 0.35f)
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

        public void Tick(float dt)
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
                float s = p.grow ? p.size * (1f + 1.5f * (1f - k)) : p.size * (0.35f + 0.65f * k);
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
        }
    }
}
