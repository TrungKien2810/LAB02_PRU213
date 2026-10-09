using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SnowBoarder
{
    public enum Sfx { Crash, Finish, Pickup, PowerUp, Jump, Land, Trick, Hit, Smash, Boost, Pause, Click, Checkpoint, GameOver }

    /// <summary>
    /// Plays sound effects and the background music. Crash/Finish use the lab's audio files,
    /// the rest are synthesized at startup so the project does not need extra audio assets.
    /// Also drives a wind/carving loop whose volume follows the player's speed.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        /// <summary>Raised when the player toggles mute (M), so the UI can give feedback.</summary>
        public static event System.Action<bool> MuteChanged;

        [SerializeField] AudioClip crashClip;
        [SerializeField] AudioClip finishClip;

        AudioSource sfxSource;
        AudioSource windSource;
        AudioSource musicSource;
        AudioClip pickup, powerUp, jump, land, trick, hit, smash, boost, pause, click, checkpoint, gameOver, crashFallback, finishFallback;
        float windTarget;
        float musicTarget, musicFade = 0f;

        void Awake()
        {
            Instance = this;
            GameSettings.ApplyVolume();

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.volume = 0f;
            musicSource.spatialBlend = 0f;

            windSource = gameObject.AddComponent<AudioSource>();
            windSource.clip = SfxSynth.Noise("Wind", 2f, 0.35f);
            windSource.loop = true;
            windSource.volume = 0f;
            windSource.Play();

            pickup = SfxSynth.Arpeggio("Pickup", new[] { 988f, 1319f }, 0.07f, 0.35f);
            powerUp = SfxSynth.Arpeggio("PowerUp", new[] { 523f, 659f, 784f, 1047f }, 0.07f, 0.35f);
            jump = SfxSynth.Sweep("Jump", 300f, 700f, 0.15f, 0.3f);
            land = SfxSynth.NoiseBurst("Land", 0.12f, 0.5f);
            trick = SfxSynth.Arpeggio("Trick", new[] { 784f, 988f, 1175f, 1568f }, 0.06f, 0.35f);
            hit = SfxSynth.Sweep("Hit", 220f, 90f, 0.18f, 0.5f);
            click = SfxSynth.Arpeggio("Click", new[] { 880f }, 0.04f, 0.25f);
            checkpoint = SfxSynth.Arpeggio("Checkpoint", new[] { 659f, 880f }, 0.1f, 0.35f);
            gameOver = SfxSynth.Arpeggio("GameOver", new[] { 392f, 330f, 262f }, 0.18f, 0.35f);
            smash = SfxSynth.NoiseBurst("Smash", 0.3f, 0.7f);
            boost = SfxSynth.Sweep("Boost", 260f, 1300f, 0.35f, 0.35f);
            pause = SfxSynth.Arpeggio("Pause", new[] { 660f, 440f }, 0.06f, 0.3f);

            // fallbacks so the game still has a crash / finish cue if the lab clips are unassigned
            crashFallback = SfxSynth.NoiseBurst("CrashSynth", 0.45f, 0.8f);
            finishFallback = SfxSynth.Arpeggio("FinishSynth", new[] { 784f, 988f, 1319f, 1568f }, 0.12f, 0.4f);
        }

        void Start() => StartCoroutine(PrepareMusicRoutine());

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            windSource.volume = Mathf.MoveTowards(windSource.volume, windTarget * GameSettings.SfxVolume, dt);
            windSource.pitch = 0.8f + windTarget * 0.6f;

            // music fades in when it is ready and out when the track is being rebuilt
            musicFade = Mathf.MoveTowards(musicFade, musicTarget, dt / 1.2f);
            musicSource.volume = musicFade * GameSettings.MusicVolume;

            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame)
            {
                GameSettings.ToggleMute();
                MuteChanged?.Invoke(GameSettings.Muted);
            }
        }

        /// <summary>0..1 intensity for the wind/carving loop.</summary>
        public void SetWind(float intensity) => windTarget = Mathf.Clamp01(intensity) * 0.6f;

        // ---------------- music ----------------
        /// <summary>
        /// Composes the loop for this scene (menu = calm theme, levels = slope theme) and
        /// renders it in small chunks so the procedural synthesis never stalls a frame.
        /// </summary>
        IEnumerator PrepareMusicRoutine()
        {
            bool menu = SceneManager.GetActiveScene().name == LevelCatalog.MainMenuScene;
            int level = 0;
            for (int i = 0; i < LevelCatalog.Levels.Length; i++)
                if (LevelCatalog.Levels[i].SceneName == SceneManager.GetActiveScene().name) level = i;

            var track = menu ? MusicSynth.MenuTheme() : MusicSynth.SlopeTheme(level);
            var clip = AudioClip.Create(menu ? "Menu Theme" : "Slope Theme", track.Length, 1, MusicSynth.Rate, false);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            int pos = 0, block = MusicSynth.Rate / 20; // 50 ms of audio per render step
            while (pos < track.Length)
            {
                watch.Restart();
                while (pos < track.Length && watch.ElapsedMilliseconds < 6)
                {
                    track.Render(pos, Mathf.Min(pos + block, track.Length));
                    pos += block;
                }
                yield return null;
            }

            track.Normalize(0.85f);
            clip.SetData(track.Buffer, 0);

            musicSource.clip = clip;
            musicSource.Play();
            musicTarget = 1f;
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            AudioClip clip = sfx switch
            {
                Sfx.Crash => crashClip ? crashClip : crashFallback,
                Sfx.Finish => finishClip ? finishClip : finishFallback,
                Sfx.Pickup => pickup,
                Sfx.PowerUp => powerUp,
                Sfx.Jump => jump,
                Sfx.Land => land,
                Sfx.Trick => trick,
                Sfx.Hit => hit,
                Sfx.Smash => smash,
                Sfx.Boost => boost,
                Sfx.Pause => pause,
                Sfx.Click => click,
                Sfx.Checkpoint => checkpoint,
                Sfx.GameOver => gameOver,
                _ => null
            };
            if (clip != null) sfxSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        public static void PlaySfx(Sfx sfx, float volume = 1f)
        {
            if (Instance != null) Instance.Play(sfx, volume);
        }
    }

    /// <summary>Tiny procedural sound generator.</summary>
    public static class SfxSynth
    {
        const int Rate = 44100;

        public static AudioClip Arpeggio(string name, float[] notes, float noteLen, float vol)
        {
            int perNote = (int)(Rate * noteLen);
            int tail = (int)(Rate * 0.15f);
            var data = new float[perNote * notes.Length + tail];
            for (int n = 0; n < notes.Length; n++)
            {
                int len = perNote + tail;
                for (int i = 0; i < len && n * perNote + i < data.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 18f);
                    float s = Mathf.Sin(2 * Mathf.PI * notes[n] * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * notes[n] * t);
                    data[n * perNote + i] += s * env * vol;
                }
            }
            return Make(name, data);
        }

        public static AudioClip Sweep(string name, float from, float to, float len, float vol)
        {
            int count = (int)(Rate * len);
            var data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float k = i / (float)count;
                phase += 2 * Mathf.PI * Mathf.Lerp(from, to, k) / Rate;
                data[i] = Mathf.Sin(phase) * (1 - k) * vol;
            }
            return Make(name, data);
        }

        public static AudioClip NoiseBurst(string name, float len, float vol)
        {
            int count = (int)(Rate * len);
            var data = new float[count];
            var rng = new System.Random(1);
            float lp = 0f;
            for (int i = 0; i < count; i++)
            {
                float k = i / (float)count;
                lp = Mathf.Lerp(lp, (float)(rng.NextDouble() * 2 - 1), 0.25f);
                data[i] = lp * (1 - k) * (1 - k) * vol;
            }
            return Make(name, data);
        }

        /// <summary>Seamless filtered noise loop (wind / snow carving).</summary>
        public static AudioClip Noise(string name, float len, float vol)
        {
            int count = (int)(Rate * len);
            var data = new float[count];
            var rng = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < count; i++)
            {
                lp = Mathf.Lerp(lp, (float)(rng.NextDouble() * 2 - 1), 0.05f);
                data[i] = lp * vol * 3f;
            }
            int fade = Rate / 10;
            for (int i = 0; i < fade; i++)
            {
                float a = i / (float)fade;
                data[i] = data[i] * a + data[count - fade + i] * (1 - a);
            }
            System.Array.Resize(ref data, count - fade);
            return Make(name, data);
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>
    /// Tiny procedural composer: builds a looping background track out of scheduled
    /// "notes" (simple oscillators with attack / decay / release envelopes), so the
    /// project needs no music files. Every voice is stateless, which lets AudioManager
    /// render the loop in small chunks instead of stalling a frame.
    /// </summary>
    public static class MusicSynth
    {
        public const int Rate = 44100;

        public enum Wave { Sine, Tri, Hollow, Pluck, Fizz, Wind, Kick }

        public struct Note
        {
            public int Start, Len;              // samples
            public int Seed;                    // noise seed per voice
            public float Freq, Freq2;           // Hz (Freq2 = sweep target, or noise rate for Wind)
            public float Vol;
            public Wave Wave;
            public float Attack, Decay, Release; // seconds
        }

        /// <summary>A scheduled arrangement plus the buffer it is rendered into.</summary>
        public class Track
        {
            readonly Note[] notes;
            public readonly float[] Buffer;

            public Track(Note[] notes, int length)
            {
                this.notes = notes;
                Buffer = new float[length];
            }

            public int Length => Buffer.Length;

            /// <summary>Adds every note that overlaps [from, to) into the buffer.</summary>
            public void Render(int from, int to)
            {
                for (int n = 0; n < notes.Length; n++)
                {
                    Note note = notes[n];
                    int a = Mathf.Max(from, note.Start);
                    int b = Mathf.Min(to, note.Start + note.Len);
                    if (a >= b) continue;

                    double w = 2 * System.Math.PI * note.Freq / Rate;
                    double len = note.Len / (double)Rate;
                    for (int i = a; i < b; i++)
                    {
                        double t = (i - note.Start) / (double)Rate;
                        double env = note.Attack > 0.001 ? System.Math.Min(1.0, t / note.Attack) : 1.0;
                        if (note.Decay > 0.001) env *= System.Math.Exp(-t / note.Decay);
                        if (note.Release > 0.001 && t > len - note.Release)
                            env *= System.Math.Max(0.0, (len - t) / note.Release);
                        if (env < 0.0002) continue;

                        Buffer[i] += (float)(Sample(note, i - note.Start, w) * env * note.Vol);
                    }
                }
            }

            /// <summary>Scales the whole loop so its loudest peak sits at <paramref name="peak"/>.</summary>
            public void Normalize(float peak)
            {
                float loudest = 0f;
                for (int i = 0; i < Buffer.Length; i++) loudest = Mathf.Max(loudest, Mathf.Abs(Buffer[i]));
                if (loudest < 0.0001f) return;
                float gain = peak / loudest;
                for (int i = 0; i < Buffer.Length; i++) Buffer[i] *= gain;
            }

            static double Sample(Note note, int local, double w)
            {
                double t = local / (double)Rate;
                double p = w * local;
                switch (note.Wave)
                {
                    case Wave.Tri:
                        return 2.0 / System.Math.PI * System.Math.Asin(System.Math.Sin(p));
                    case Wave.Hollow: // soft square-ish bass (odd harmonics only)
                        return 0.7 * (System.Math.Sin(p) + 0.35 * System.Math.Sin(3 * p) + 0.18 * System.Math.Sin(5 * p));
                    case Wave.Pluck:  // bright plucked string
                        return 0.75 * (System.Math.Sin(p) + 0.45 * System.Math.Sin(2 * p));
                    case Wave.Fizz:   // high-passed value noise = hi-hat
                        {
                            int i = note.Start + local;
                            return 0.6 * (Hash(note.Seed, i) - Hash(note.Seed, i - 1));
                        }
                    case Wave.Wind:   // two octaves of smooth noise = snow / wind bed
                        {
                            double x = (note.Start + local) / (double)Rate * System.Math.Max(4.0, note.Freq);
                            return 2.0 * (ValueNoise(note.Seed, x) * 0.75 + ValueNoise(note.Seed + 91, x * 2.7) * 0.25);
                        }
                    case Wave.Kick:   // sine sweeping down (drum)
                        {
                            double span = note.Len / (double)Rate;
                            double f1 = note.Freq2 > 0f ? note.Freq2 : note.Freq * 0.35;
                            double phase = 2 * System.Math.PI * (note.Freq * t + (f1 - note.Freq) * t * t / (2.0 * span));
                            return System.Math.Sin(phase);
                        }
                    default:
                        return System.Math.Sin(p);
                }
            }
        }

        /// <summary>Schedules notes, dropping any that would run past the loop point.</summary>
        class Score
        {
            public int Length;
            public readonly List<Note> Notes = new List<Note>();

            public void Add(double time, double len, double freq, float vol, Wave wave,
                            double attack = 0.005, double decay = 0.0, double release = 0.03, double freq2 = 0.0)
            {
                var note = new Note
                {
                    Start = (int)(time * Rate),
                    Len = (int)(len * Rate),
                    Seed = Notes.Count * 7919 + 13,
                    Freq = (float)freq,
                    Freq2 = (float)freq2,
                    Vol = vol,
                    Wave = wave,
                    Attack = (float)attack,
                    Decay = (float)decay,
                    Release = (float)release,
                };
                if (note.Start < 0 || note.Len <= 0 || note.Start + note.Len > Length) return;
                Notes.Add(note);
            }

            public Track Build() => new Track(Notes.ToArray(), Length);
        }

        static double Freq(int midi) => 440.0 * System.Math.Pow(2.0, (midi - 69) / 12.0);

        static float Hash(int seed, int i)
        {
            uint h = (uint)(i * 374761393 + seed * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / (float)0xFFFFFF * 2f - 1f;
        }

        static double ValueNoise(int seed, double x)
        {
            int i = (int)System.Math.Floor(x);
            double f = x - i;
            double s = f * f * (3 - 2 * f);
            return Hash(seed, i) + (Hash(seed, i + 1) - Hash(seed, i)) * s;
        }

        /// <summary>Calm, airy loop for the main menu: Am7 · Fmaj7 · Cmaj7 · G6.</summary>
        public static Track MenuTheme()
        {
            const double beat = 60.0 / 80;      // 80 bpm
            const double bar = beat * 4;
            const int bars = 8;
            var s = new Score { Length = (int)(bar * bars * Rate) };

            int[][] chords =
            {
                new[] { 57, 60, 64, 67 },       // Am7
                new[] { 53, 57, 60, 64 },       // Fmaj7
                new[] { 48, 52, 55, 59 },       // Cmaj7
                new[] { 55, 59, 62, 64 },       // G6
            };
            int[] bass = { 45, 41, 36, 43 };
            int[] bellPattern = { 0, 2, 1, 3, 2, 0, 3, 1 };

            // pads, one chord every two bars
            for (int c = 0; c < chords.Length; c++)
                foreach (int midi in chords[c])
                    s.Add(c * 2 * bar, 2 * bar - 0.1, Freq(midi), 0.20f, Wave.Sine, 1.1, 0, 1.2);

            for (int b = 0; b < bars; b++)
            {
                var chord = chords[b / 2];

                // slow bass on beats 1 and 3
                s.Add(b * bar, 2.6, Freq(bass[b / 2]), 0.30f, Wave.Tri, 0.02, 0.9, 0.4);
                s.Add(b * bar + 2 * beat, 2.2, Freq(bass[b / 2]), 0.20f, Wave.Tri, 0.02, 0.8, 0.4);

                // sparse bell arpeggio an octave above the pad
                for (int k = 0; k < bellPattern.Length; k++)
                    s.Add(b * bar + (0.5 + k * 0.5) * beat, 2.4, Freq(chord[bellPattern[k]] + 12), 0.085f,
                          Wave.Sine, 0.01, 0.85, 0.3);
            }

            // snow / wind bed breathing through the loop
            s.Add(0, bars * bar - 0.15, 26, 0.09f, Wave.Wind, 1.5, 0, 2.0);
            s.Add(0, bars * bar - 0.15, 90, 0.05f, Wave.Wind, 1.5, 0, 2.0);
            return s.Build();
        }

        /// <summary>Driving level loop; later mountains play faster and darker.</summary>
        public static Track SlopeTheme(int level)
        {
            double beat = 60.0 / (100 + Mathf.Clamp(level, 0, 2) * 8);
            double bar = beat * 4;
            const int bars = 8;
            var s = new Score { Length = (int)(bar * bars * Rate) };

            // A minor walk, or E minor for the storm peak
            int[][] chords = level >= 2
                ? new[] { new[] { 52, 55, 59 }, new[] { 48, 52, 55 }, new[] { 50, 55, 59 }, new[] { 50, 54, 57 } }
                : new[] { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 60, 64 }, new[] { 55, 59, 62 } };
            int[] roots = level >= 2 ? new[] { 40, 36, 43, 38 } : new[] { 45, 41, 48, 43 };
            int[] arp = { 0, 1, 2, 1, 0, 2, 1, 2 };

            for (int b = 0; b < bars; b++)
            {
                var chord = chords[b % chords.Length];
                double t0 = b * bar;

                // drums: kick on every beat, hi-hat on the offbeats
                for (int k = 0; k < 4; k++)
                {
                    s.Add(t0 + k * beat, 0.16, 130, 0.55f, Wave.Kick, 0.002, 0.075, 0.03, 48);
                    s.Add(t0 + (k + 0.5) * beat, 0.06, 7000, 0.10f, Wave.Fizz, 0.001, 0.02, 0.012);
                }

                // bass: driving eighths on the root
                for (int k = 0; k < 8; k++)
                {
                    if (k % 2 == 1) continue;
                    s.Add(t0 + k * (beat / 2), 0.26, Freq(roots[b % roots.Length]), 0.34f, Wave.Hollow, 0.005, 0.12, 0.06);
                }

                // 16th note pluck arpeggio over the chord
                for (int k = 0; k < 16; k++)
                    s.Add(t0 + k * (beat / 4), 0.28, Freq(chord[arp[k % arp.Length]] + 12),
                          k % 4 == 2 ? 0.07f : 0.11f, Wave.Pluck, 0.004, 0.10, 0.04);

                // sustained pad under everything (ends before the bar line)
                foreach (int midi in chord)
                    s.Add(t0, bar - 0.35, Freq(midi), 0.10f, Wave.Sine, 0.25, 0, 0.3);
            }

            s.Add(0, bars * bar - 0.2, 34, 0.06f, Wave.Wind, 0.8, 0, 0.8);
            return s.Build();
        }
    }
}
