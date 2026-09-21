using System.Collections.Generic;
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
        AudioSource music;      // the plucked motif, one note at a time
        AudioSource hiss;       // embers and acid near the wanderer
        AudioClip place, remove, success, fail, reveal, rewind, select, blocked, jump;
        AudioClip crack, thud, whoosh, bubble, stingerStart, stingerDeath, doorChime, voiceBlip;
        AudioClip hissLoop, bubbleLoop;
        readonly AudioClip[] steps = new AudioClip[2];
        readonly AudioClip[] ticks = new AudioClip[16];

        bool motifOn;
        float motifGain = 1f;

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
            doorChime = Arpeggio("door", new[] { 783.99f, 1174.66f, 1567.98f }, 0.09f, 0.8f, 0.22f);
            stingerStart = Arpeggio("start", new[] { 220f, 329.63f, 440f }, 0.16f, 0.9f, 0.26f);
            stingerDeath = Sweep("death", 0.7f, 220f, 55f, 0.3f);
            crack = Noise("crack", 0.12f, 0.5f, 0.35f);
            thud = Sweep("thud", 0.18f, 120f, 40f, 0.5f);
            whoosh = Noise("whoosh", 0.55f, 0.45f, 0.12f, rising: true);
            bubble = Sweep("bubble", 0.08f, 180f, 420f, 0.14f);
            voiceBlip = Tone("blip", 0.035f, 1046f, 0.10f, Wave.Triangle);
            hissLoop = Noise("hiss", 1.5f, 0.35f, 0.08f);
            bubbleLoop = Bubbles("bubbles", 2.4f);

            pad = gameObject.AddComponent<AudioSource>();
            pad.playOnAwake = false;
            pad.spatialBlend = 0f;
            pad.loop = true;
            pad.volume = 0.14f;
            pad.clip = Pad("pad", 6f);

            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.volume = 0.55f;

            hiss = gameObject.AddComponent<AudioSource>();
            hiss.playOnAwake = false;
            hiss.spatialBlend = 0f;
            hiss.loop = true;
            hiss.volume = 0f;
            hiss.clip = hissLoop;
        }

        public void StartAmbient()
        {
            if (!pad.isPlaying) pad.Play();
        }

        // ------------------------------------------------------------------ the music: a 16-bit tracker

        enum Voice { Pulse12, Pulse25, Pulse50, Tri, Fm, Kick, Hat }

        /// <summary>A channel: one voice, one token per sixteenth: a note name (C5), '-' holds, '.' rests.</summary>
        struct Track
        {
            public Voice voice;
            public float gain;
            public string[] bars;
        }

        // Pastoral, in C: a lead that leans on the 6th and the 4th, bell arps under it, a walking triangle bass,
        // a soft kick and hat. Eight bars of four, 100 beats a minute for the chapters, 76 for the title.
        static readonly string[] LeadBars =
        {
            "E5 - - - G5 - A5 - G5 - - - E5 - D5 -",
            "C5 - - - - - D5 - E5 - - - - - - .",
            "G5 - - - A5 - C6 - A5 - - - G5 - E5 -",
            "D5 - - - - - E5 - D5 - - - - - - .",
            "E5 - - - G5 - A5 - G5 - - - E5 - D5 -",
            "C5 - - - - - D5 - E5 - - - G5 - A5 -",
            "G5 - - - E5 - D5 - C5 - - - - - D5 -",
            "C5 - - - - - - - - - - - . . . .",
        };
        static readonly string[] CounterBars =
        {
            ". . . . . . . . C5 - - - . . . .", ". . . . E4 - - - . . . . G4 - - -",
            ". . . . . . . . E5 - - - . . . .", ". . . . G4 - - - . . . . B4 - - -",
            ". . . . . . . . C5 - - - . . . .", ". . . . E4 - - - . . . . A4 - - -",
            ". . . . . . . . F4 - - - G4 - - -", "E4 - - - - - - - . . . . . . . .",
        };
        static readonly int[][] Chords =   // per bar: root, third, fifth (MIDI), C Am F G C Am F G
        {
            new[] { 48, 52, 55 }, new[] { 45, 48, 52 }, new[] { 41, 45, 48 }, new[] { 43, 47, 50 },
            new[] { 48, 52, 55 }, new[] { 45, 48, 52 }, new[] { 41, 45, 48 }, new[] { 43, 47, 50 },
        };

        static readonly Dictionary<int, AudioClip> songs = new Dictionary<int, AudioClip>();
        AudioClip Song(int transpose, bool title)
        {
            int key = transpose * 2 + (title ? 1 : 0);
            if (songs.TryGetValue(key, out var clip)) return clip;
            clip = RenderSong(transpose, title);
            songs[key] = clip;
            return clip;
        }

        static string[] ArpBars(bool title)
        {
            var bars = new string[8];
            for (int b = 0; b < 8; b++)
            {
                var c = Chords[b];
                var t = new string[16];
                for (int i = 0; i < 16; i++)
                {
                    // eighths: root, fifth, octave, fifth; the title arpeggiates in quarters
                    int slot = title ? i / 4 : i / 2;
                    bool hit = title ? i % 4 == 0 : i % 2 == 0;
                    int[] shape = { c[0] + 12, c[2] + 12, c[0] + 24, c[1] + 12 };
                    t[i] = hit ? Name(shape[slot % 4]) : "-";
                }
                bars[b] = string.Join(" ", t);
            }
            return bars;
        }

        static string[] BassBars(bool title)
        {
            var bars = new string[8];
            for (int b = 0; b < 8; b++)
            {
                var c = Chords[b];
                var t = new string[16];
                for (int i = 0; i < 16; i++) t[i] = "-";
                t[0] = Name(c[0] - 12);
                if (!title) { t[6] = Name(c[0] - 12); t[8] = Name(c[2] - 12); t[12] = Name(b % 2 == 0 ? c[0] - 12 : c[1] - 12); }
                else t[8] = Name(c[2] - 12);
                bars[b] = string.Join(" ", t);
            }
            return bars;
        }

        static string[] DrumBars(Voice v, bool title)
        {
            var bars = new string[8];
            string kick = "C2 . . . . . . . C2 . . . . . C2 .";
            string hat = ". . C6 . . . C6 . . . C6 . . . C6 .";
            for (int b = 0; b < 8; b++) bars[b] = v == Voice.Kick ? (title ? "C2 . . . . . . . . . . . . . . ." : kick) : (title ? ". . . . . . . . . . . . . . . ." : hat);
            return bars;
        }

        static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        static string Name(int midi) => NoteNames[((midi % 12) + 12) % 12] + (midi / 12 - 1);
        static int Midi(string name)
        {
            int i = 0;
            while (i < name.Length && !char.IsDigit(name[i]) && name[i] != '-') i++;
            int n = System.Array.IndexOf(NoteNames, name.Substring(0, i));
            return (int.Parse(name.Substring(i)) + 1) * 12 + n;
        }

        static float Osc(Voice v, float phase, float t, float freq)
        {
            float p = phase - Mathf.Floor(phase);
            switch (v)
            {
                case Voice.Pulse12: return p < 0.125f ? 1f : -1f;
                case Voice.Pulse25: return p < 0.25f ? 1f : -1f;
                case Voice.Pulse50: return p < 0.5f ? 1f : -1f;
                case Voice.Tri: return 1f - 4f * Mathf.Abs(p - 0.5f);
                case Voice.Fm:
                {
                    // two operators: a carrier bent by a modulator an octave up, the bend fading with the note
                    float index = 1.8f * Mathf.Exp(-3f * t);
                    return Mathf.Sin(2f * Mathf.PI * p + index * Mathf.Sin(4f * Mathf.PI * p));
                }
                default: return 0f;
            }
        }

        /// <summary>Render the whole loop to one clip: every channel summed, then 8-bit quantised and low-passed.</summary>
        AudioClip RenderSong(int transpose, bool title)
        {
            float bpm = title ? 76f : 100f;
            float step = 60f / bpm / 4f;
            int stepsTotal = 8 * 16;
            int n = (int)(stepsTotal * step * Rate);
            var mix = new float[n];
            var tracks = new List<Track>
            {
                new Track { voice = Voice.Pulse25, gain = title ? 0.20f : 0.24f, bars = LeadBars },
                new Track { voice = Voice.Pulse12, gain = 0.12f, bars = CounterBars },
                new Track { voice = Voice.Fm, gain = title ? 0.16f : 0.12f, bars = ArpBars(title) },
                new Track { voice = Voice.Tri, gain = 0.30f, bars = BassBars(title) },
                new Track { voice = Voice.Kick, gain = 0.32f, bars = DrumBars(Voice.Kick, title) },
                new Track { voice = Voice.Hat, gain = 0.06f, bars = DrumBars(Voice.Hat, title) },
            };
            var rng = new System.Random(3);
            foreach (var tr in tracks)
            {
                var tokens = new List<string>();
                foreach (var bar in tr.bars) tokens.AddRange(bar.Split(' '));
                // notes: (start step, length in steps, midi)
                for (int s = 0; s < tokens.Count; s++)
                {
                    string tok = tokens[s];
                    if (tok == "-" || tok == ".") continue;
                    int len = 1;
                    while (s + len < tokens.Count && tokens[s + len] == "-") len++;
                    int midi = Midi(tok) + (tr.voice == Voice.Kick || tr.voice == Voice.Hat ? 0 : transpose);
                    float freq = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
                    int start = (int)(s * step * Rate);
                    float noteSeconds = len * step;
                    float tail = tr.voice == Voice.Fm ? 0.35f : 0.06f;
                    int count = (int)((noteSeconds + tail) * Rate);
                    float phase = 0f;
                    for (int i = 0; i < count && start + i < n; i++)
                    {
                        float t = (float)i / Rate;
                        float env;
                        if (tr.voice == Voice.Kick) env = Mathf.Exp(-18f * t);
                        else if (tr.voice == Voice.Hat) env = Mathf.Exp(-60f * t);
                        else
                        {
                            float on = Mathf.Min(1f, t / 0.006f) * (0.65f + 0.35f * Mathf.Exp(-9f * t));
                            float off = t > noteSeconds ? Mathf.Exp(-(t - noteSeconds) / (tail * 0.35f)) : 1f;
                            env = on * off;
                        }
                        float sample;
                        if (tr.voice == Voice.Kick)
                        {
                            phase += Mathf.Lerp(150f, 45f, Mathf.Min(1f, t * 12f)) / Rate;
                            sample = Mathf.Sin(2f * Mathf.PI * phase);
                        }
                        else if (tr.voice == Voice.Hat) sample = (float)rng.NextDouble() * 2f - 1f;
                        else
                        {
                            float vib = tr.voice == Voice.Pulse25 && t > 0.18f ? 1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 5.5f * t) : 1f;
                            phase += freq * vib / Rate;
                            sample = Osc(tr.voice, phase, t, freq);
                        }
                        mix[start + i] += sample * env * tr.gain;
                    }
                }
            }
            // lo-fi: 8-bit steps, then a gentle low-pass, then a touch of the pad's warmth below
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float v = Mathf.Clamp(mix[i], -1f, 1f);
                v = Mathf.Round(v * 96f) / 96f;
                y += 0.42f * (v - y);
                mix[i] = y * 0.85f;
            }
            return Make("song" + transpose + (title ? "t" : ""), mix);
        }

        /// <summary>Which music plays: the chapter's transposition of the tune, or the title's slow take.</summary>
        public void SetMotif(int chapter, bool brighter, int seed)
        {
            int[] keys = { 0, 2, -3, 4, -1, 5, 3, -2, 7, 2 };   // a different key per chapter, none too far
            SetSong(keys[((chapter % keys.Length) + keys.Length) % keys.Length], chapter < 0);
        }

        public void SetTitleMusic() => SetSong(0, true);

        void SetSong(int transpose, bool title)
        {
            var clip = Song(transpose, title);
            if (music.clip == clip && music.isPlaying) return;
            music.clip = clip;
            music.loop = true;
            music.Play();
        }

        public void SetMotifPlaying(bool on, float gain = 1f)
        {
            motifOn = on;
            motifGain = gain;
            if (!on && music.isPlaying) music.Pause();
            if (on && !music.isPlaying && music.clip != null) music.UnPause();
            music.volume = 0.55f * musicLevel * motifGain;
        }

        /// <summary>Near-field hazards: the hiss of embers and the bubbling of acid, by closeness (0..1).</summary>
        public void SetNearby(float ember, float acidNear)
        {
            float v = Mathf.Max(ember * 0.5f, acidNear * 0.4f);
            var clip = acidNear > ember ? bubbleLoop : hissLoop;
            if (hiss.clip != clip) { hiss.clip = clip; if (v > 0f) hiss.Play(); }
            if (v > 0.01f && !hiss.isPlaying) hiss.Play();
            if (v <= 0.01f && hiss.isPlaying) hiss.Stop();
            hiss.volume = Mathf.Lerp(hiss.volume, v, 0.15f);
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

        float musicLevel = 1f;

        public void SetMusicVolume(float v)
        {
            musicLevel = Mathf.Clamp01(v);
            if (pad != null) pad.volume = 0.14f * musicLevel;
            if (music != null) music.volume = 0.55f * musicLevel * motifGain;
        }

        public void Crack() => src.PlayOneShot(crack);
        public void Thud() => src.PlayOneShot(thud);
        public void Whoosh() => src.PlayOneShot(whoosh);
        public void Bubble() => src.PlayOneShot(bubble, 0.6f);
        public void StingerStart() => src.PlayOneShot(stingerStart);
        public void StingerDeath() => src.PlayOneShot(stingerDeath);
        public void DoorChime() => src.PlayOneShot(doorChime);
        public void VoiceBlip(int n) => src.PlayOneShot(voiceBlip, 0.5f + 0.2f * ((n * 7) % 3));

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

        /// <summary>A plucked string: a bright attack that dulls as it decays, with a touch of the octave.</summary>
        static AudioClip Pluck(string name, float seconds, float freq)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-4.5f * t / seconds);
                float bright = Mathf.Exp(-9f * t / seconds);
                float s = Mathf.Sin(t * freq * 2f * Mathf.PI)
                        + 0.35f * bright * Mathf.Sin(t * freq * 4f * Mathf.PI)
                        + 0.12f * bright * Mathf.Sin(t * freq * 6f * Mathf.PI);
                data[i] = s * env * 0.4f;
            }
            return Make(name, data);
        }

        /// <summary>Filtered white noise with a decaying (or rising) envelope: cracks, whooshes, hisses.</summary>
        static AudioClip Noise(string name, float seconds, float volume, float lowpass, bool rising = false)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, k = t / seconds;
                float white = (float)rng.NextDouble() * 2f - 1f;
                y += lowpass * (white - y);
                float env = rising ? Mathf.Sin(k * Mathf.PI) : Mathf.Exp(-5f * k);
                data[i] = y * env * volume * 3f;
            }
            return Make(name, data);
        }

        /// <summary>A loop of soft blubs at uneven intervals, for acid.</summary>
        static AudioClip Bubbles(string name, float seconds)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(7);
            int at = 0;
            while (at < n)
            {
                float f0 = 140f + (float)rng.NextDouble() * 120f;
                int len = (int)(0.07f * Rate);
                float phase = 0f;
                for (int i = 0; i < len && at + i < n; i++)
                {
                    float t = (float)i / Rate, k = t / 0.07f;
                    phase += Mathf.Lerp(f0, f0 * 2.4f, k * k) / Rate;
                    data[at + i] += Mathf.Sin(phase * 2f * Mathf.PI) * (1f - k) * 0.5f;
                }
                at += len + (int)((0.12f + (float)rng.NextDouble() * 0.35f) * Rate);
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
