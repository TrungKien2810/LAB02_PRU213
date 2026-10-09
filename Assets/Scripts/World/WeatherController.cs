using UnityEngine;
using UnityEngine.UI;

namespace SnowBoarder
{
    /// <summary>
    /// Dynamic weather: falling snow that follows the camera, wind gusts that
    /// change the snowfall direction/intensity, and a fog overlay whose
    /// opacity changes over time (changing visibility, e.g. blizzards).
    /// </summary>
    public class WeatherController : MonoBehaviour
    {
        [SerializeField] Material snowMaterial;
        [SerializeField] Image fogOverlay;
        [SerializeField] float baseSnowRate = 60f;
        [SerializeField] float gustSnowRate = 150f;
        [SerializeField] float baseWind = -2f;
        [SerializeField] float gustWind = -10f;
        [SerializeField, Range(0, 1)] float baseFog = 0f;
        [SerializeField, Range(0, 1)] float gustFog = 0.25f;
        [SerializeField] float gustPeriod = 14f;

        ParticleSystem snow;
        ParticleSystem.EmissionModule emission;
        ParticleSystem.VelocityOverLifetimeModule velocity;
        Camera cam;

        /// <summary>0..1 current storm strength (shown on HUD).</summary>
        public float Gust { get; private set; }

        void Start()
        {
            cam = Camera.main;
            if (!GameSettings.WeatherEffects)
            {
                if (fogOverlay) fogOverlay.enabled = false;
                enabled = false;
                return;
            }
            CreateSnow();
        }

        void CreateSnow()
        {
            var go = new GameObject("Snowfall");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(4f, 16f, 10f);
            snow = go.AddComponent<ParticleSystem>();
            snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = snow.main;
            main.loop = true;
            main.startLifetime = 5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.4f);
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;
            main.gravityModifier = 0.05f;

            var shape = snow.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 1f, 1f);
            shape.rotation = new Vector3(90f, 0f, 0f);

            emission = snow.emission;
            emission.rateOverTime = baseSnowRate;

            velocity = snow.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(baseWind);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            var noise = snow.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = snowMaterial;
            r.sortingOrder = 50;

            snow.Simulate(4f, true, true);
            snow.Play();
        }

        void Update()
        {
            // smooth gusts from layered perlin noise + slow sine cycle
            float t = Time.time;
            float cycle = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI * 2f / gustPeriod) * 0.6f + 0.2f);
            float noise = Mathf.PerlinNoise(t * 0.15f, 0.37f);
            Gust = Mathf.Clamp01(cycle * 0.7f + noise * 0.5f - 0.15f);

            emission.rateOverTime = Mathf.Lerp(baseSnowRate, baseSnowRate + gustSnowRate, Gust);
            velocity.x = new ParticleSystem.MinMaxCurve(Mathf.Lerp(baseWind, baseWind + gustWind, Gust));

            if (fogOverlay)
            {
                var c = fogOverlay.color;
                c.a = Mathf.Lerp(baseFog, baseFog + gustFog, Gust);
                fogOverlay.color = c;
            }
        }
    }
}
