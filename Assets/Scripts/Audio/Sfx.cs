using UnityEngine;

namespace Snake
{
    /// <summary>Chiptune-style sound effects synthesised at startup, so there are no audio files.</summary>
    public class Sfx : MonoBehaviour
    {
        enum Wave { Sine, Triangle, Square }

        const int Rate = 44100;

        public bool Enabled = true;

        public AudioClip Eat, Level, Die, Bonk, Click, Beep, Go, Pop;

        AudioSource[] voices;
        int next;

        void Awake()
        {
            voices = new AudioSource[8];
            for (int i = 0; i < voices.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                voices[i] = s;
            }

            Eat = Sweep("eat", 0.11f, 520f, 1040f, Wave.Triangle, 20f, 0.6f);
            Level = Notes("level", new[] { 392f, 523.3f, 659.3f, 784f, 1046.5f, 1318.5f }, 0.075f, Wave.Triangle, 7f, 0.5f);
            Die = Sweep("die", 0.75f, 420f, 50f, Wave.Square, 3f, 0.55f, 0.3f);
            Bonk = Sweep("bonk", 0.14f, 180f, 90f, Wave.Sine, 25f, 0.9f);
            Click = Sweep("click", 0.04f, 1400f, 1200f, Wave.Sine, 60f, 0.35f);
            Beep = Sweep("beep", 0.1f, 660f, 660f, Wave.Sine, 18f, 0.5f);
            Go = Sweep("go", 0.16f, 990f, 1320f, Wave.Triangle, 12f, 0.5f);
            Pop = Sweep("pop", 0.06f, 300f, 760f, Wave.Sine, 45f, 0.5f);
        }

        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (!Enabled || clip == null) return;
            var v = voices[next];
            next = (next + 1) % voices.Length;
            v.Stop();
            v.clip = clip;
            v.volume = volume;
            v.pitch = pitch;
            v.Play();
        }

        static AudioClip Sweep(string name, float dur, float f0, float f1, Wave wave, float decay, float vol, float noise = 0f)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            var data = new float[n];
            var rng = new System.Random(n);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(f0, f1, t / dur);
                phase += 2.0 * System.Math.PI * f / Rate;
                float s = Osc(wave, phase);
                if (noise > 0f) s = s * (1f - noise) + (float)(rng.NextDouble() * 2.0 - 1.0) * noise;
                data[i] = s * Envelope(t, dur, decay) * vol;
            }
            return Make(name, data);
        }

        static AudioClip Notes(string name, float[] freqs, float noteDur, Wave wave, float decay, float vol)
        {
            const float tail = 0.15f;
            int n = Mathf.CeilToInt((freqs.Length * noteDur + tail) * Rate);
            var data = new float[n];
            float len = noteDur + tail;
            for (int k = 0; k < freqs.Length; k++)
            {
                int start = Mathf.FloorToInt(k * noteDur * Rate);
                double phase = 0;
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate;
                    if (t > len) break;
                    phase += 2.0 * System.Math.PI * freqs[k] / Rate;
                    data[i] += Osc(wave, phase) * Envelope(t, len, decay) * vol;
                }
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            return Make(name, data);
        }

        static float Envelope(float t, float dur, float decay)
            => Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * decay) * Mathf.Clamp01((dur - t) / 0.01f);

        static float Osc(Wave wave, double phase)
        {
            float s = (float)System.Math.Sin(phase);
            switch (wave)
            {
                case Wave.Triangle: return (float)(2.0 / System.Math.PI * System.Math.Asin(s));
                case Wave.Square: return Mathf.Clamp(s * 3f, -1f, 1f) * 0.6f;
                default: return s;
            }
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
