using UnityEngine;

namespace PointOfOrigin
{
    /// <summary>Every sound in the game, synthesised at start-up: no audio assets.</summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;

        enum Wave { Sine, Triangle, Saw }

        AudioSource src;
        AudioSource pad;
        AudioClip place, remove, success, fail, reveal, rewind, select, blocked, jump;
        readonly AudioClip[] steps = new AudioClip[2];
        readonly AudioClip[] ticks = new AudioClip[16];

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0.8f;

            place = Tone("place", 0.09f, 880f, 0.35f, Wave.Sine);
            remove = Tone("remove", 0.09f, 440f, 0.30f, Wave.Sine);
            select = Tone("select", 0.07f, 660f, 0.25f, Wave.Triangle);
            reveal = Tone("reveal", 0.30f, 660f, 0.25f, Wave.Triangle);
            fail = Tone("fail", 0.35f, 98f, 0.40f, Wave.Saw);
            rewind = Sweep("rewind", 0.28f, 640f, 160f, 0.22f);
            blocked = Tone("blocked", 0.09f, 140f, 0.22f, Wave.Saw);
            jump = Sweep("jump", 0.12f, 320f, 720f, 0.16f);
            steps[0] = Tone("step0", 0.03f, 620f, 0.07f, Wave.Triangle);
            steps[1] = Tone("step1", 0.03f, 700f, 0.07f, Wave.Triangle);
            for (int i = 0; i < ticks.Length; i++)
                ticks[i] = Tone("tick" + i, 0.06f, 262f * Mathf.Pow(1.0595f, i), 0.22f, Wave.Triangle);
            success = Arpeggio("success", new[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.11f, 0.55f, 0.30f);

            pad = gameObject.AddComponent<AudioSource>();
            pad.playOnAwake = false;
            pad.spatialBlend = 0f;
            pad.loop = true;
            pad.volume = 0.32f;
            pad.clip = Pad("pad", 6f);
        }

        public void StartAmbient()
        {
            if (!pad.isPlaying) pad.Play();
        }

        float master = 1f;
        bool mutedNow;

        public void SetMuted(bool muted)
        {
            mutedNow = muted;
            AudioListener.volume = muted ? 0f : master;
        }

        public void SetMasterVolume(float v)
        {
            master = Mathf.Clamp01(v);
            AudioListener.volume = mutedNow ? 0f : master;
        }

        public void SetMusicVolume(float v)
        {
            if (pad != null) pad.volume = 0.32f * Mathf.Clamp01(v);
        }

        public void Place() => src.PlayOneShot(place);
        public void Remove() => src.PlayOneShot(remove);
        public void Select() => src.PlayOneShot(select);
        public void Reveal() => src.PlayOneShot(reveal);
        public void Rewind() => src.PlayOneShot(rewind);
        public void Blocked() => src.PlayOneShot(blocked);
        public void Jump() => src.PlayOneShot(jump);
        public void Step(int parity) => src.PlayOneShot(steps[parity & 1]);
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

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
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
            return Make(name, data);
        }

        /// <summary>A sine whose pitch slides from one frequency to another.</summary>
        static AudioClip Sweep(string name, float seconds, float from, float to, float volume)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float k = t / seconds;
                float freq = Mathf.Lerp(from, to, k * k);
                phase += freq / Rate;
                float env = Mathf.Min(1f, t / 0.004f) * (1f - k);
                data[i] = Mathf.Sin(phase * 2f * Mathf.PI) * env * volume;
            }
            return Make(name, data);
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
            return Make(name, data);
        }

        /// <summary>
        /// A quiet drone that loops seamlessly: every partial completes a whole
        /// number of cycles over the clip, and so does the slow swell.
        /// </summary>
        static AudioClip Pad(string name, float seconds)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            float[] freqs = { 55f, 55f + 1f / seconds, 82.5f, 110f, 165f };
            float[] amps = { 0.34f, 0.30f, 0.16f, 0.11f, 0.04f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float swell = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * t / seconds);
                float s = 0f;
                for (int k = 0; k < freqs.Length; k++) s += amps[k] * Mathf.Sin(2f * Mathf.PI * freqs[k] * t);
                data[i] = s * swell * 0.55f;
            }
            return Make(name, data);
        }
    }
}
