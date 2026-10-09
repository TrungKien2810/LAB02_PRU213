using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnowBoarder.EditorTools
{
    public enum SegmentType { Start, Slope, Rollers, Kicker, Landing, Cliff, Uphill, Runout }

    public class Segment
    {
        public SegmentType Type;
        public float StartX, EndX;
        public float Size;          // kicker size / cliff drop
        public Vector2 Lip;         // launch point for kickers/cliffs
        public float Length => EndX - StartX;
    }

    /// <summary>
    /// Builds a downhill profile by integrating a slope (derivative) function made of segments.
    /// Integrating slopes keeps the surface continuous, and blending slopes keeps it smooth,
    /// while kickers keep a sharp lip so the boarder launches into the air.
    /// </summary>
    public class TrackDesigner
    {
        const float Dx = 0.5f;

        readonly List<float> slopes = new List<float>();
        public readonly List<Segment> Segments = new List<Segment>();
        float lastSlope;
        readonly System.Random rng;

        public TrackDesigner(int seed) { rng = new System.Random(seed); }

        public float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        public float X => slopes.Count * Dx;

        Segment Add(SegmentType type, float len, Func<float, float> deriv)
        {
            var seg = new Segment { Type = type, StartX = X };
            int n = Mathf.Max(1, Mathf.RoundToInt(len / Dx));
            for (int i = 0; i < n; i++) slopes.Add(deriv((i + 0.5f) / n));
            lastSlope = slopes[slopes.Count - 1];
            seg.EndX = X;
            Segments.Add(seg);
            return seg;
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        static float SlopeOf(float deg) => -Mathf.Tan(deg * Mathf.Deg2Rad); // positive deg = downhill

        /// <summary>Flat area behind the start, then rolls into a gentle slope so the rider gets going.</summary>
        public void Start(float len, float rollDeg = 9f)
        {
            float s1 = SlopeOf(rollDeg);
            Add(SegmentType.Start, len, t => Mathf.Lerp(0f, s1, Smooth((t - 0.45f) / 0.4f)));
        }

        public Segment Slope(float len, float deg, SegmentType type = SegmentType.Slope, float blend = 0.3f)
        {
            float s0 = lastSlope, s1 = SlopeOf(deg);
            return Add(type, len, t => Mathf.Lerp(s0, s1, Smooth(t / blend)));
        }

        public Segment Rollers(float len, float deg, float amp, int waves)
        {
            float s0 = lastSlope, s1 = SlopeOf(deg);
            return Add(SegmentType.Rollers, len, t =>
                Mathf.Lerp(s0, s1, Smooth(t / 0.15f)) + amp * Mathf.Sin(t * waves * Mathf.PI * 2f) * Smooth(t / 0.1f));
        }

        public Segment Uphill(float len, float deg)
        {
            float s0 = lastSlope, s1 = Mathf.Tan(deg * Mathf.Deg2Rad);
            return Add(SegmentType.Uphill, len, t => Mathf.Lerp(s0, s1, Smooth(t / 0.4f)));
        }

        /// <summary>Approach slope, curved ramp, gap drop and a long landing slope.</summary>
        public Segment Kicker(float size)
        {
            Slope(8f, 16f + size * 2f);
            float s0 = lastSlope;
            float lipSlope = Mathf.Tan((16f + size * 7f) * Mathf.Deg2Rad);
            var ramp = Add(SegmentType.Kicker, 3f + size * 1.5f, t => Mathf.Lerp(s0, lipSlope, Mathf.Pow(t, 1.4f)));
            ramp.Size = size;
            ramp.Lip = new Vector2(X, 0f); // y filled in after integration
            float drop = -1.8f - size * 0.4f;
            Add(SegmentType.Cliff, 2.5f + size * 1.5f, t => Mathf.Lerp(drop * 0.6f, drop, t));
            Slope(14f + size * 5f, 26f + size * 2f, SegmentType.Landing, 0.35f);
            return ramp;
        }

        /// <summary>Flat lip then a steep drop: big air without a ramp.</summary>
        public Segment Cliff(float dropHeight)
        {
            var lip = Slope(5f, 3f);
            lip.Lip = new Vector2(X, 0f);
            lip.Size = dropHeight;
            float steep = -2.75f;
            Add(SegmentType.Cliff, dropHeight / -steep, _ => steep);
            Slope(16f + dropHeight, 30f, SegmentType.Landing, 0.4f);
            return lip;
        }

        public void Valley(float len, float deg, float upDeg)
        {
            Slope(len, deg);
            Uphill(len * 0.45f, upDeg);
            Slope(6f, 4f);
        }

        public void Runout(float len)
        {
            float s0 = lastSlope;
            Add(SegmentType.Runout, len, t => Mathf.Lerp(s0, 0f, Smooth(t / 0.3f)));
            Uphill(14f, 30f);
            Add(SegmentType.Runout, 10f, _ => 2.5f); // stopping wall
        }

        /// <summary>Integrates the slopes into surface points and fills lip heights.</summary>
        public Vector2[] Build(Vector2 origin)
        {
            // light smoothing of the derivative (except it keeps the kicker lip mostly intact)
            var sm = new float[slopes.Count];
            for (int i = 0; i < slopes.Count; i++)
            {
                float sum = 0f; int c = 0;
                for (int k = -1; k <= 1; k++)
                {
                    int j = i + k;
                    if (j < 0 || j >= slopes.Count) continue;
                    sum += slopes[j]; c++;
                }
                sm[i] = sum / c;
            }

            var pts = new Vector2[sm.Length + 1];
            pts[0] = origin;
            for (int i = 0; i < sm.Length; i++)
                pts[i + 1] = pts[i] + new Vector2(Dx, sm[i] * Dx);
            heights = pts;

            foreach (var s in Segments)
                if (s.Lip != Vector2.zero || s.Type == SegmentType.Kicker)
                    s.Lip = new Vector2(s.Lip.x + origin.x, HeightAt(s.Lip.x + origin.x));
            originX = origin.x;
            return pts;
        }

        Vector2[] heights;
        float originX;

        public float HeightAt(float worldX)
        {
            float f = (worldX - heights[0].x) / Dx;
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, heights.Length - 2);
            return Mathf.Lerp(heights[i].y, heights[i + 1].y, Mathf.Clamp01(f - i));
        }

        public float AngleAt(float worldX)
        {
            float a = HeightAt(worldX - 0.6f), b = HeightAt(worldX + 0.6f);
            return Mathf.Atan2(b - a, 1.2f) * Mathf.Rad2Deg;
        }

        public float WorldStart(Segment s) => s.StartX + originX;
        public float WorldEnd(Segment s) => s.EndX + originX;
    }
}
