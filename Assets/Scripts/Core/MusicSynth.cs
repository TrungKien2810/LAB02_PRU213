using System.Collections.Generic;
using UnityEngine;

namespace SnowBoarder
{
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

            /// <summary>
            /// Adds every note that overlaps [from, to) into the buffer. A note that rings past
            /// the loop point wraps to the start, so the tail of the last bar is heard under the
            /// first one and the loop has no gap.
            /// </summary>
            public void Render(int from, int to)
            {
                for (int n = 0; n < notes.Length; n++)
                {
                    RenderSpan(notes[n], from, to, 0);
                    RenderSpan(notes[n], from, to, Length);
                }
            }

            /// <summary>Renders the part of a note that lands in [from, to) once shifted back by <paramref name="shift"/>.</summary>
            void RenderSpan(Note note, int from, int to, int shift)
            {
                int start = note.Start - shift;
                int a = Mathf.Max(from, start);
                int b = Mathf.Min(to, start + note.Len);
                if (a >= b) return;

                double w = 2 * System.Math.PI * note.Freq / Rate;
                double len = note.Len / (double)Rate;
                for (int i = a; i < b; i++)
                {
                    int local = i - start;
                    double t = local / (double)Rate;
                    double env = note.Attack > 0.001 ? System.Math.Min(1.0, t / note.Attack) : 1.0;
                    if (note.Decay > 0.001) env *= System.Math.Exp(-t / note.Decay);
                    if (note.Release > 0.001 && t > len - note.Release)
                        env *= System.Math.Max(0.0, (len - t) / note.Release);
                    if (env < 0.0002) continue;

                    Buffer[i] += (float)(Sample(note, local, w) * env * note.Vol);
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

        /// <summary>Schedules notes. One that runs past the loop point wraps to the start (see <see cref="Track.Render"/>).</summary>
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
                if (note.Start < 0 || note.Start >= Length || note.Len <= 0 || note.Len > Length) return;
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
