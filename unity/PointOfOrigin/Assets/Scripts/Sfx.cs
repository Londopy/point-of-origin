using UnityEngine;

namespace PointOfOrigin
{
    /// <summary>Every sound in the game, synthesised at start-up: no audio assets.</summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;

        enum Wave { Sine, Triangle, Saw }

        AudioSource src;
        AudioClip place, remove, success, fail, reveal;
        readonly AudioClip[] ticks = new AudioClip[16];

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0.8f;

            place = Tone("place", 0.09f, 880f, 0.35f, Wave.Sine);
            remove = Tone("remove", 0.09f, 440f, 0.30f, Wave.Sine);
            reveal = Tone("reveal", 0.30f, 660f, 0.25f, Wave.Triangle);
            fail = Tone("fail", 0.35f, 98f, 0.40f, Wave.Saw);
            for (int i = 0; i < ticks.Length; i++)
                ticks[i] = Tone("tick" + i, 0.06f, 262f * Mathf.Pow(1.0595f, i), 0.22f, Wave.Triangle);
            success = Arpeggio("success", new[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.11f, 0.55f, 0.30f);
        }

        public void Place() => src.PlayOneShot(place);
        public void Remove() => src.PlayOneShot(remove);
        public void Reveal() => src.PlayOneShot(reveal);
        public void Fail() => src.PlayOneShot(fail);
        public void Success() => src.PlayOneShot(success);
        public void Tick(int generation) => src.PlayOneShot(ticks[Mathf.Clamp(generation, 0, ticks.Length - 1)]);

        static float Sample(Wave w, float phase)
        {
            switch (w)
            {
                case Wave.Sine: return Mathf.Sin(phase * 2f * Mathf.PI);
                case Wave.Triangle: return 1f - 4f * Mathf.Abs(phase - 0.5f);
                default: return 2f * phase - 1f;
            }
        }

        static AudioClip Tone(string name, float seconds, float freq, float volume, Wave w)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-5f * t / seconds);
                data[i] = Sample(w, (t * freq) % 1f) * env * volume;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Arpeggio(string name, float[] freqs, float gap, float noteSeconds, float volume)
        {
            float total = gap * freqs.Length + noteSeconds;
            int n = (int)(total * Rate);
            var data = new float[n];
            for (int k = 0; k < freqs.Length; k++)
            {
                int start = (int)(k * gap * Rate);
                int len = (int)(noteSeconds * Rate);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = (float)i / Rate;
                    float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-4f * t / noteSeconds);
                    data[start + i] += Mathf.Sin(t * freqs[k] * 2f * Mathf.PI) * env * volume / freqs.Length * 2f;
                }
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
