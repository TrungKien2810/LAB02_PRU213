using UnityEngine;

namespace SnowBoarder
{
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
}
