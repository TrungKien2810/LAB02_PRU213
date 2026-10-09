using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SnowBoarder.EditorTools
{
    public static partial class SnowBoarderBuilder
    {
        class Theme
        {
            public Color Sky, SkyTint, Light, TerrainTop, TerrainBottom, Mountain, Fog;
            public float LightIntensity = 1f;
            public string SkySprite = "Sky_Day";
            public bool Sun = true;
            public Color CloudTint = Color.white;
            public float SnowBase, SnowGust, WindBase, WindGust, FogBase, FogGust, GustPeriod = 14f;
            public float RockChance, TreeChance;
            public float LaunchSpeed;
            public (float at, PowerUpType type)[] PowerUps;
        }

        static Theme ThemeFor(int level)
        {
            switch (level)
            {
                case 0:
                    return new Theme
                    {
                        Sky = new Color(0.55f, 0.8f, 0.98f), SkyTint = new Color(0.7f, 0.88f, 1f), Light = Color.white,
                        TerrainTop = Color.white, TerrainBottom = new Color(0.45f, 0.6f, 0.9f), Mountain = Color.white, Fog = Color.white,
                        SnowBase = 25f, SnowGust = 60f, WindBase = -1f, WindGust = -4f, FogBase = 0f, FogGust = 0.08f,
                        RockChance = 0.55f, TreeChance = 0f, LaunchSpeed = 13f,
                        PowerUps = new[] { (0.15f, PowerUpType.SpeedBoost), (0.38f, PowerUpType.Magnet), (0.6f, PowerUpType.Invincibility), (0.8f, PowerUpType.ExtraLife) }
                    };
                case 1:
                    return new Theme
                    {
                        Sky = new Color(0.45f, 0.62f, 0.88f), SkyTint = new Color(0.6f, 0.75f, 0.95f), Light = new Color(1f, 0.95f, 0.9f),
                        TerrainTop = Color.white, TerrainBottom = new Color(0.35f, 0.45f, 0.8f), Mountain = new Color(0.9f, 0.92f, 1f), Fog = Color.white,
                        SnowBase = 50f, SnowGust = 140f, WindBase = -2f, WindGust = -8f, FogBase = 0.03f, FogGust = 0.22f,
                        RockChance = 0.6f, TreeChance = 0.45f, LaunchSpeed = 14f,
                        SkySprite = "Sky_Dusk", CloudTint = new Color(1f, 0.9f, 0.88f),
                        PowerUps = new[] { (0.12f, PowerUpType.Magnet), (0.3f, PowerUpType.SpeedBoost), (0.47f, PowerUpType.Invincibility), (0.62f, PowerUpType.TimeBonus), (0.78f, PowerUpType.ExtraLife), (0.9f, PowerUpType.SpeedBoost) }
                    };
                default:
                    return new Theme
                    {
                        Sky = new Color(0.55f, 0.6f, 0.7f), SkyTint = new Color(0.7f, 0.74f, 0.82f), Light = new Color(0.85f, 0.9f, 1f), LightIntensity = 0.9f,
                        TerrainTop = new Color(0.92f, 0.95f, 1f), TerrainBottom = new Color(0.3f, 0.35f, 0.6f), Mountain = new Color(0.75f, 0.8f, 0.9f), Fog = new Color(0.92f, 0.95f, 1f),
                        SnowBase = 120f, SnowGust = 380f, WindBase = -5f, WindGust = -16f, FogBase = 0.1f, FogGust = 0.5f, GustPeriod = 18f,
                        RockChance = 0.65f, TreeChance = 0.6f, LaunchSpeed = 15f,
                        SkySprite = "Sky_Storm", Sun = false, CloudTint = new Color(0.72f, 0.76f, 0.86f),
                        PowerUps = new[] { (0.1f, PowerUpType.Invincibility), (0.25f, PowerUpType.Magnet), (0.4f, PowerUpType.TimeBonus), (0.52f, PowerUpType.ExtraLife), (0.66f, PowerUpType.SpeedBoost), (0.8f, PowerUpType.Invincibility), (0.9f, PowerUpType.TimeBonus) }
                    };
            }
        }

        static void DesignTrack(int level, TrackDesigner d)
        {
            switch (level)
            {
                case 0:
                    d.Start(44); d.Slope(35, 12); d.Kicker(0); d.Slope(25, 14); d.Rollers(40, 10, 0.3f, 3);
                    d.Kicker(1); d.Slope(30, 16); d.Valley(26, 20, 10); d.Kicker(1); d.Rollers(36, 12, 0.28f, 2);
                    d.Slope(30, 13); d.Kicker(0); d.Rollers(45, 11, 0.32f, 4); d.Valley(28, 18, 9);
                    d.Slope(22, 15); d.Kicker(2); d.Slope(30, 14); d.Rollers(35, 10, 0.3f, 2); d.Kicker(1);
                    d.Slope(30, 14); d.Runout(45);
                    break;
                case 1:
                    d.Start(44); d.Slope(30, 15); d.Kicker(1); d.Rollers(40, 14, 0.4f, 3); d.Slope(25, 18); d.Cliff(6);
                    d.Rollers(30, 14, 0.42f, 2); d.Kicker(1); d.Valley(30, 22, 12); d.Kicker(2); d.Slope(35, 18);
                    d.Rollers(40, 15, 0.45f, 4); d.Cliff(9); d.Kicker(2); d.Slope(30, 16); d.Valley(30, 20, 12);
                    d.Rollers(45, 14, 0.45f, 4); d.Kicker(1); d.Slope(28, 18); d.Cliff(7); d.Rollers(35, 15, 0.4f, 3);
                    d.Kicker(2); d.Slope(30, 16); d.Runout(45);
                    break;
                default:
                    d.Start(44); d.Slope(25, 18); d.Kicker(1); d.Slope(30, 22); d.Cliff(10); d.Rollers(45, 18, 0.5f, 4);
                    d.Kicker(2); d.Valley(32, 26, 14); d.Kicker(3); d.Slope(30, 22); d.Cliff(14); d.Rollers(40, 18, 0.55f, 3);
                    d.Kicker(2); d.Slope(25, 24); d.Kicker(3); d.Rollers(30, 16, 0.45f, 2); d.Valley(34, 24, 14);
                    d.Cliff(12); d.Rollers(45, 18, 0.55f, 5); d.Kicker(2); d.Slope(30, 22); d.Kicker(3);
                    d.Rollers(35, 16, 0.5f, 3); d.Cliff(16); d.Slope(30, 20); d.Runout(50);
                    break;
            }
        }

        // =====================================================================
        // Level scenes
        // =====================================================================
        static void BuildLevel(int level)
        {
            var info = LevelCatalog.Levels[level];
            var theme = ThemeFor(level);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- track ---
            var d = new TrackDesigner(1234 + level * 77);
            DesignTrack(level, d);
            var pts = d.Build(new Vector2(-30f, 0f));

            var terrainGo = new GameObject("Terrain");
            var edge = terrainGo.AddComponent<EdgeCollider2D>();
            edge.sharedMaterial = snowPhysics;
            terrainGo.AddComponent<Ground>();
            var terrain = terrainGo.AddComponent<TerrainGenerator>();
            Set(terrain, "fillMaterial", fillMat);
            Set(terrain, "edgeMaterial", edgeMat);
            SetColor(terrain, "topColor", theme.TerrainTop);
            SetColor(terrain, "bottomColor", theme.TerrainBottom);
            terrain.SetPoints(pts);

            // --- systems ---
            var systems = new GameObject("Game Systems");
            var audio = systems.AddComponent<AudioManager>();
            Set(audio, "crashClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Lab2Assets/Audio/Crash SFX.ogg"));
            Set(audio, "finishClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Lab2Assets/Audio/Finish SFX.ogg"));
            var gm = systems.AddComponent<GameManager>();
            var weather = systems.AddComponent<WeatherController>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = theme.Light;
            light.intensity = theme.LightIntensity;

            // --- player ---
            float startX = -6f;
            var startPoint = new GameObject("Start Point").transform;
            startPoint.position = new Vector3(startX, d.HeightAt(startX) + 1.3f, 0f);
            var player = Place(playerPrefab, null, startPoint.position).GetComponent<PlayerController>();

            // --- camera ---
            var cam = MakeCamera(theme, player.transform.position + new Vector3(5f, 2f, -10f));
            var follow = cam.gameObject.AddComponent<CameraFollow>();
            Set(follow, "target", player);
            BuildBackground(cam, theme, player.transform.position);

            // --- level content ---
            var content = new GameObject("Level Content").transform;
            PopulateTrack(level, d, theme, content);

            // --- HUD ---
            var hud = BuildHUD(out var fog);
            fog.color = new Color(theme.Fog.r, theme.Fog.g, theme.Fog.b, theme.FogBase);

            Set(weather, "snowMaterial", snowMat);
            Set(weather, "fogOverlay", fog);
            SetFloat(weather, "baseSnowRate", theme.SnowBase);
            SetFloat(weather, "gustSnowRate", theme.SnowGust);
            SetFloat(weather, "baseWind", theme.WindBase);
            SetFloat(weather, "gustWind", theme.WindGust);
            SetFloat(weather, "baseFog", theme.FogBase);
            SetFloat(weather, "gustFog", theme.FogGust);
            SetFloat(weather, "gustPeriod", theme.GustPeriod);
            Set(hud, "weather", weather);

            SetInt(gm, "levelIndex", level);
            Set(gm, "player", player);
            Set(gm, "hud", hud);
            Set(gm, "startPoint", startPoint);

            EditorSceneManager.SaveScene(scene, SceneDir + info.SceneName + ".unity");
        }

        static Camera MakeCamera(Theme theme, Vector3 pos)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = pos;
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = theme.Sky;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();

            // 32x512 px gradient at 100 PPU, stretched over the whole view (20 units tall, up to ~21:9 wide)
            var sky = SpriteObj("Sky Gradient", BG(theme.SkySprite), -100, camGo.transform);
            sky.transform.localPosition = new Vector3(0, 0, 50f);
            sky.transform.localScale = new Vector3(150f, 4.4f, 1f);
            if (theme.Sun)
            {
                var sun = SpriteObj("Sun", BG("Sun"), -90, camGo.transform);
                sun.transform.localPosition = new Vector3(11f, 5.5f, 50f);
                sun.transform.localScale = Vector3.one * 1.3f;
            }
            return cam;
        }

        static void BuildBackground(Camera cam, Theme theme, Vector3 origin)
        {
            var bg = new GameObject("Background").transform;
            // y offsets are relative to the camera start height; every layer follows the camera vertically
            float camY = cam.transform.position.y;
            BackgroundLayer(bg, "Mountains Far", BG("MountainsFar"), -40, 2.0f, origin.x, camY + 1f, 0.95f, theme.Mountain, null);
            BackgroundLayer(bg, "Mountains Mid", BG("MountainsMid"), -38, 1.8f, origin.x, camY - 2.5f, 0.88f, theme.Mountain, null);
            BackgroundLayer(bg, "Hills", BG("Hills"), -36, 1.6f, origin.x, camY - 4.5f, 0.75f, theme.Mountain, new Color(225f / 255f, 234f / 255f, 247f / 255f));

            var clouds = new GameObject("Clouds (parallax)");
            clouds.transform.SetParent(bg, false);
            clouds.transform.position = new Vector3(origin.x, camY + 6f, 0f);
            string[] cloudSprites = { "Clouds_A", "Clouds_B", "Clouds_C" };
            var rnd = new System.Random(11);
            const float cloudSpacing = 14f;
            for (int i = 0; i < 6; i++)
            {
                var c = SpriteObj("Cloud", BG(cloudSprites[i % 3]), -30, clouds.transform);
                float sc = 0.6f + (float)rnd.NextDouble() * 0.5f;
                c.transform.localScale = new Vector3(sc, sc, 1f);
                c.transform.localPosition = new Vector3(-42f + i * cloudSpacing + (float)rnd.NextDouble() * 4f, (float)rnd.NextDouble() * 3f - 0.5f, 0f);
                c.GetComponent<SpriteRenderer>().color = theme.CloudTint;
            }
            var pc = clouds.AddComponent<Parallax>();
            SetVector2(pc, "factor", new Vector2(0.97f, 1f));
            SetFloat(pc, "tileWidth", cloudSpacing);
        }

        /// <summary>A horizontally tiling parallax strip (5 copies of a seamless sprite).</summary>
        static void BackgroundLayer(Transform parent, string name, Sprite sprite, int order, float scale, float x, float y, float factor, Color tint, Color? baseColor)
        {
            var layer = new GameObject(name + " (parallax)");
            layer.transform.SetParent(parent, false);
            layer.transform.position = new Vector3(x, y, 0f);
            float tile = sprite.bounds.size.x * scale;
            float h = sprite.bounds.size.y * scale;
            for (int i = -2; i <= 2; i++)
            {
                var m = SpriteObj(name, sprite, order, layer.transform);
                m.transform.localScale = new Vector3(scale, scale, 1f);
                m.transform.localPosition = new Vector3(i * tile, 0f, 0f);
                m.GetComponent<SpriteRenderer>().color = tint;
                if (baseColor.HasValue)
                {
                    // solid fill below the strip so its bottom edge never shows
                    var b = SpriteObj("Base", Gen("Square"), order, m.transform);
                    b.transform.localPosition = new Vector3(0f, -(h / scale) * 0.5f - 4.9f, 0f);
                    b.transform.localScale = new Vector3((tile / scale + 0.1f) / 0.16f, 10f / 0.16f, 1f);
                    b.GetComponent<SpriteRenderer>().color = baseColor.Value * tint;
                }
            }
            var p = layer.AddComponent<Parallax>();
            SetVector2(p, "factor", new Vector2(factor, 1f));
            SetFloat(p, "tileWidth", tile);
        }

        static void PopulateTrack(int level, TrackDesigner d, Theme theme, Transform content)
        {
            var rnd = new System.Random(99 + level);
            float R() => (float)rnd.NextDouble();

            var flakes = new GameObject("Snowflakes").transform; flakes.SetParent(content);
            var obstacles = new GameObject("Obstacles").transform; obstacles.SetParent(content);
            var power = new GameObject("PowerUps").transform; power.SetParent(content);
            var decor = new GameObject("Decor Trees").transform; decor.SetParent(content);
            var pads = new GameObject("Boost Pads").transform; pads.SetParent(content);

            void Flake(float x, float yOffset) => Place(snowflakePrefab, flakes, new Vector3(x, d.HeightAt(x) + yOffset, 0f));

            int kickerCount = 0;
            foreach (var s in d.Segments)
            {
                float x0 = d.WorldStart(s), x1 = d.WorldEnd(s);

                // snowflake arcs over jumps follow an estimated launch trajectory
                if (s.Lip != Vector2.zero)
                {
                    bool isCliff = s.Type != SegmentType.Kicker;
                    float angle = isCliff ? -3f : 16f + s.Size * 7f;
                    float v = theme.LaunchSpeed + s.Size;
                    float vx = v * Mathf.Cos(angle * Mathf.Deg2Rad), vy = v * Mathf.Sin(angle * Mathf.Deg2Rad);
                    int n = 6 + (int)s.Size;
                    for (int i = 1; i <= n; i++)
                    {
                        float t = i * 0.11f;
                        float x = s.Lip.x + vx * t;
                        float y = s.Lip.y + 1.2f + vy * t - 0.5f * 19.62f * t * t;
                        if (y < d.HeightAt(x) + 1.2f) break;
                        Place(snowflakePrefab, flakes, new Vector3(x, y, 0f));
                    }
                    // boost pad on the approach of every other kicker
                    if (!isCliff && (kickerCount++ % 2 == 1 || s.Size >= 2))
                    {
                        float px = x0 - 6f;
                        Place(boostPrefab, pads, new Vector3(px, d.HeightAt(px) + 0.15f, 0f), d.AngleAt(px));
                    }
                }

                if (s.Type == SegmentType.Slope || s.Type == SegmentType.Rollers)
                {
                    if (s.Length < 16f) continue;
                    // a line of snowflakes
                    float fx = x0 + 5f;
                    for (int i = 0; i < 5; i++) Flake(fx + i * 2.2f, 1.4f);

                    // obstacles in the second half of the segment
                    if (R() < theme.RockChance)
                    {
                        float rx = Mathf.Lerp(x0, x1, 0.55f + R() * 0.25f);
                        var prefab = R() < 0.5f ? rockPrefab : rock2Prefab;
                        Place(prefab, obstacles, new Vector3(rx, d.HeightAt(rx) + 0.2f, 0f), d.AngleAt(rx));
                    }
                    if (R() < theme.TreeChance)
                    {
                        float tx = Mathf.Lerp(x0, x1, 0.82f + R() * 0.1f);
                        Place(treePrefab, obstacles, new Vector3(tx, d.HeightAt(tx) + 1.0f, 0f));
                        // reward flakes above the tree for jumping over it
                        for (int i = -1; i <= 1; i++) Flake(tx + i * 1.4f, 3.4f - Mathf.Abs(i) * 0.5f);
                    }
                }
                else if (s.Type == SegmentType.Landing)
                {
                    float lx = x1 - 6f;
                    for (int i = 0; i < 3; i++) Flake(lx + i * 2f, 1.4f);
                }
            }

            // power-ups at fixed fractions of the run
            float startX = d.WorldStart(d.Segments[0]);
            float endX = d.WorldStart(d.Segments.Find(s => s.Type == SegmentType.Runout));
            foreach (var (at, type) in theme.PowerUps)
            {
                float x = Mathf.Lerp(startX + 45f, endX, at);
                Place(powerUpPrefabs[type], power, new Vector3(x, d.HeightAt(x) + 2.2f, 0f));
            }

            // checkpoints
            for (int i = 1; i <= 2; i++)
            {
                float x = Mathf.Lerp(startX, endX, i / 3f);
                // nudge onto a slope (not a jump)
                var seg = d.Segments.Find(s => d.WorldEnd(s) > x && (s.Type == SegmentType.Slope || s.Type == SegmentType.Rollers));
                if (seg != null) x = Mathf.Max(x, d.WorldStart(seg) + 4f);
                var cp = Place(checkpointPrefab, content, new Vector3(x, d.HeightAt(x), 0f));
                cp.name = "Checkpoint " + i;
            }

            // finish line
            BuildFinishLine(content, new Vector3(endX + 8f, d.HeightAt(endX + 8f), 0f));

            // decorative background trees (no colliders)
            float dx = startX - 20f;
            while (dx < endX + 60f)
            {
                float s = 0.1f + R() * 0.12f;
                var t = SpriteObj("Tree", Lab(R() < 0.5f ? "Snow-Tree-1" : "Snow-Tree-2"), -6, decor);
                t.transform.localScale = new Vector3(s * (R() < 0.5f ? 1 : -1), s, 1f);
                t.transform.position = new Vector3(dx, d.HeightAt(dx) - 0.8f + 12f * s, 0f);
                float shade = 0.75f + R() * 0.2f;
                t.GetComponent<SpriteRenderer>().color = new Color(shade, shade + 0.05f, Mathf.Min(1f, shade + 0.15f));
                dx += 6f + R() * 18f;
            }
        }

        static void BuildFinishLine(Transform parent, Vector3 pos)
        {
            var root = new GameObject("Finish Line");
            root.transform.SetParent(parent);
            root.transform.position = pos;
            for (int side = -1; side <= 1; side += 2)
            {
                var pole = SpriteObj("Pole", Gen("Square"), 2, root.transform);
                pole.transform.localPosition = new Vector3(side * 2.2f, 2.5f, 0f);
                pole.transform.localScale = new Vector3(0.15f / 0.16f, 6f / 0.16f, 1f);
                pole.GetComponent<SpriteRenderer>().color = new Color(0.85f, 0.15f, 0.2f);
            }
            var banner = SpriteObj("Banner", Gen("Checker"), 3, root.transform);
            banner.transform.localPosition = new Vector3(0f, 5.3f, 0f);
            banner.transform.localScale = new Vector3(1.8f, 1.6f, 1f);

            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1f, 30f);
            box.offset = new Vector2(0f, 10f);

            var fx = MakeParticles("Finish Confetti", root.transform, new Vector3(0f, 5.5f, 0f), 30);
            var m = fx.main;
            m.loop = false; m.playOnAwake = false; m.duration = 1f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            var colors = new Gradient();
            colors.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.33f),
                                   new GradientColorKey(new Color(0.3f, 0.8f, 1f), 0.66f), new GradientColorKey(new Color(0.5f, 1f, 0.4f), 1f) },
                           new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            m.startColor = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };
            m.gravityModifier = 1f;
            var sh = fx.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var em = fx.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 150) });

            var fl = root.AddComponent<FinishLine>();
            Set(fl, "finishEffect", fx);
        }

        static void SetVector2(Object target, string field, Vector2 v)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).vector2Value = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        // HUD
        // =====================================================================
        static HUDController BuildHUD(out Image fog)
        {
            var canvas = MakeCanvas("HUD Canvas");
            var t = canvas.transform;
            var hud = canvas.gameObject.AddComponent<HUDController>();

            fog = UIImage("Fog Overlay", t, Gen("Square"), Color.clear);
            Stretch(fog.rectTransform);
            fog.raycastTarget = false;

            // top-left: score
            var scoreBox = UIImage("Score Box", t, UI("PanelHUD"), new Color(0.08f, 0.16f, 0.36f, 0.72f));
            scoreBox.type = Image.Type.Sliced;
            Anchor(scoreBox.rectTransform, 0, 1, new Vector2(30, -30), new Vector2(430, 170));
            UIText("Score Label", scoreBox.transform, "SCORE", 24, TextAnchor.UpperLeft, new Color(0.7f, 0.85f, 1f), 0, 1, new Vector2(24, -12), new Vector2(380, 30));
            var score = UIText("Score", scoreBox.transform, "0", 64, TextAnchor.UpperLeft, Color.white, 0, 1, new Vector2(22, -38), new Vector2(390, 74));
            score.fontStyle = FontStyle.Bold;
            var flakeIcon = UIImage("Flake Icon", scoreBox.transform, UI("Icon_Snowflake"), Color.white);
            Anchor(flakeIcon.rectTransform, 0, 1, new Vector2(18, -112), new Vector2(46, 46));
            var flakes = UIText("Snowflakes", scoreBox.transform, "0/0", 30, TextAnchor.MiddleLeft, Color.white, 0, 1, new Vector2(70, -116), new Vector2(200, 44));
            var levelText = UIText("Level", t, "", 22, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.85f), 0, 1, new Vector2(34, -206), new Vector2(800, 30));

            // top-right: time & lives
            var timeBox = UIImage("Time Box", t, UI("PanelHUD"), new Color(0.08f, 0.16f, 0.36f, 0.72f));
            timeBox.type = Image.Type.Sliced;
            Anchor(timeBox.rectTransform, 1, 1, new Vector2(-30, -30), new Vector2(360, 170));
            var time = UIText("Time", timeBox.transform, "0:00.00", 48, TextAnchor.UpperRight, Color.white, 1, 1, new Vector2(-24, -14), new Vector2(330, 90));
            time.fontStyle = FontStyle.Bold;
            var timerIcon = UIImage("Timer Icon", timeBox.transform, UI("Icon_Timer"), Color.white);
            Anchor(timerIcon.rectTransform, 0, 1, new Vector2(16, -14), new Vector2(64, 64));
            var lives = new GameObject("Lives", typeof(RectTransform));
            lives.transform.SetParent(timeBox.transform, false);
            Anchor((RectTransform)lives.transform, 1, 1, new Vector2(-24, -104), new Vector2(330, 52));
            var hearts = new List<Image>();
            for (int i = 0; i < 6; i++)
            {
                var h = UIImage("Heart " + (i + 1), lives.transform, UI("Icon_Heart"), Color.white);
                Anchor(h.rectTransform, 1, 0.5f, new Vector2(-i * 54, 0), new Vector2(50, 50));
                hearts.Add(h);
            }

            // top-centre: combo
            var combo = new GameObject("Combo", typeof(RectTransform));
            combo.transform.SetParent(t, false);
            Anchor((RectTransform)combo.transform, 0.5f, 1, new Vector2(0, -36), new Vector2(560, 100));
            var mult = UIText("Multiplier", combo.transform, "x1.0", 44, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.25f), 0.5f, 1, Vector2.zero, new Vector2(560, 60));
            mult.fontStyle = FontStyle.Bold;
            var comboBg = UIImage("Combo Bar BG", combo.transform, Gen("Square"), new Color(0f, 0f, 0f, 0.4f));
            Anchor(comboBg.rectTransform, 0.5f, 1, new Vector2(0, -66), new Vector2(420, 14));
            var comboFill = UIImage("Combo Bar", comboBg.transform, Gen("Square"), new Color(1f, 0.8f, 0.2f));
            Stretch(comboFill.rectTransform);
            comboFill.type = Image.Type.Filled; comboFill.fillMethod = Image.FillMethod.Horizontal;

            var weatherText = UIText("Weather Warning", t, "", 26, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.9f), 0.5f, 1, new Vector2(0, -140), new Vector2(700, 40));

            // bottom-left: speedometer
            var speedBox = UIImage("Speed Box", t, UI("PanelHUD"), new Color(0.08f, 0.16f, 0.36f, 0.72f));
            speedBox.type = Image.Type.Sliced;
            Anchor(speedBox.rectTransform, 0, 0, new Vector2(30, 30), new Vector2(420, 110));
            var speedIcon = UIImage("Speed Icon", speedBox.transform, UI("Icon_Speed"), Color.white);
            Anchor(speedIcon.rectTransform, 0, 1, new Vector2(16, -8), new Vector2(56, 56));
            var speed = UIText("Speed", speedBox.transform, "0 km/h", 40, TextAnchor.UpperLeft, Color.white, 0, 1, new Vector2(82, -10), new Vector2(320, 54));
            speed.fontStyle = FontStyle.Bold;
            var speedBg = UIImage("Speed Bar BG", speedBox.transform, Gen("Square"), new Color(0f, 0f, 0f, 0.4f));
            Anchor(speedBg.rectTransform, 0, 0, new Vector2(22, 20), new Vector2(376, 22));
            var speedFill = UIImage("Speed Bar", speedBg.transform, Gen("Square"), new Color(0.4f, 0.85f, 1f));
            Stretch(speedFill.rectTransform);
            speedFill.type = Image.Type.Filled; speedFill.fillMethod = Image.FillMethod.Horizontal;

            // left: power-ups
            var powerUps = UIText("PowerUps", t, "", 30, TextAnchor.UpperLeft, Color.white, 0, 1, new Vector2(34, -250), new Vector2(500, 200));
            powerUps.fontStyle = FontStyle.Bold;

            // bottom: controls hint
            UIText("Controls Hint", t, "←/→ rotate & flip   ↑ push   ↓ brake   SPACE jump   SHIFT grab   ESC pause   R restart   M mute",
                22, TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.75f), 0.5f, 0, new Vector2(0, 24), new Vector2(1300, 34));

            var popup = UIText("Trick Popup", t, "", 58, TextAnchor.MiddleCenter, Color.white, 0.5f, 0.5f, new Vector2(0, 230), new Vector2(1200, 180));
            popup.fontStyle = FontStyle.Bold;
            var countdown = UIText("Countdown", t, "", 160, TextAnchor.MiddleCenter, Color.white, 0.5f, 0.5f, new Vector2(0, 40), new Vector2(600, 260));
            countdown.fontStyle = FontStyle.Bold;

            // ---- pause ----
            var pause = Overlay("Pause Panel", t, "PAUSED", new Vector2(640, 560), out var pauseBox);
            var resume = UIButton("Resume", pauseBox, "RESUME", new Vector2(0, 40), new Vector2(420, 90), new Color(0.35f, 0.75f, 1f));
            var pRestart = UIButton("Restart", pauseBox, "RESTART", new Vector2(0, -70), new Vector2(420, 90), Color.white);
            var pMenu = UIButton("Main Menu", pauseBox, "MAIN MENU", new Vector2(0, -180), new Vector2(420, 90), Color.white);

            // ---- game over ----
            var over = Overlay("Game Over Panel", t, "GAME OVER", new Vector2(700, 600), out var overBox);
            var overText = UIText("Result", overBox, "", 40, TextAnchor.MiddleCenter, new Color(0.15f, 0.2f, 0.35f), 0.5f, 0.5f, new Vector2(0, 50), new Vector2(620, 150));
            var oRestart = UIButton("Try Again", overBox, "TRY AGAIN", new Vector2(0, -90), new Vector2(420, 90), new Color(0.35f, 0.75f, 1f));
            var oMenu = UIButton("Main Menu", overBox, "MAIN MENU", new Vector2(0, -200), new Vector2(420, 90), Color.white);

            // ---- level complete ----
            var done = Overlay("Level Complete Panel", t, "", new Vector2(900, 900), out var doneBox);
            var doneTitle = doneBox.Find("Title").GetComponent<Text>();
            var stars = UIText("Stars", doneBox, "", 80, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.15f), 0.5f, 1, new Vector2(0, -125), new Vector2(600, 100));
            var starImages = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                starImages[i] = UIImage("Star " + (i + 1), stars.transform, UI("Icon_Star"), Color.white);
                float big = i == 1 ? 110 : 90;
                Anchor(starImages[i].rectTransform, 0.5f, 0.5f, new Vector2((i - 1) * 115, i == 1 ? 10 : -4), new Vector2(big, big));
            }
            var doneText = UIText("Summary", doneBox, "", 32, TextAnchor.UpperCenter, new Color(0.15f, 0.2f, 0.35f), 0.5f, 1, new Vector2(0, -235), new Vector2(820, 400));
            var next = UIButton("Next", doneBox, "NEXT MOUNTAIN", new Vector2(-220, -340), new Vector2(380, 90), new Color(0.4f, 0.9f, 0.5f));
            var dRestart = UIButton("Retry", doneBox, "RETRY", new Vector2(190, -340), new Vector2(240, 90), Color.white);
            var dMenu = UIButton("Menu", doneBox, "MENU", new Vector2(190, -340), new Vector2(240, 90), Color.white);
            ((RectTransform)dRestart.transform).anchoredPosition = new Vector2(130, -370);
            ((RectTransform)dMenu.transform).anchoredPosition = new Vector2(330, -370);
            ((RectTransform)dRestart.transform).sizeDelta = new Vector2(180, 90);
            ((RectTransform)dMenu.transform).sizeDelta = new Vector2(180, 90);
            ((RectTransform)next.transform).anchoredPosition = new Vector2(-160, -370);
            ((RectTransform)next.transform).sizeDelta = new Vector2(420, 90);

            MakeEventSystem();

            Set(hud, "scoreText", score);
            Set(hud, "speedText", speed);
            Set(hud, "speedFill", speedFill);
            SetArray(hud, "hearts", hearts.ToArray());
            Set(hud, "livesGroup", lives);
            Set(hud, "multiplierText", mult);
            Set(hud, "comboFill", comboFill);
            Set(hud, "comboGroup", combo);
            Set(hud, "timeText", time);
            Set(hud, "snowflakeText", flakes);
            Set(hud, "powerUpText", powerUps);
            Set(hud, "levelText", levelText);
            Set(hud, "popupText", popup);
            Set(hud, "countdownText", countdown);
            Set(hud, "weatherText", weatherText);
            Set(hud, "pausePanel", pause);
            Set(hud, "resumeButton", resume);
            Set(hud, "pauseRestartButton", pRestart);
            Set(hud, "pauseMenuButton", pMenu);
            Set(hud, "gameOverPanel", over);
            Set(hud, "gameOverText", overText);
            Set(hud, "gameOverRestartButton", oRestart);
            Set(hud, "gameOverMenuButton", oMenu);
            Set(hud, "completePanel", done);
            Set(hud, "completeTitle", doneTitle);
            Set(hud, "completeText", doneText);
            Set(hud, "starsText", stars);
            SetArray(hud, "starImages", starImages);
            Set(hud, "starFull", UI("Icon_Star"));
            Set(hud, "starEmpty", UI("Icon_StarEmpty"));
            Set(hud, "nextButton", next);
            Set(hud, "completeRestartButton", dRestart);
            Set(hud, "completeMenuButton", dMenu);
            return hud;
        }

        /// <summary>Full-screen dim + centred card with a title.</summary>
        static GameObject Overlay(string name, Transform parent, string title, Vector2 size, out Transform box)
        {
            var dim = UIImage(name, parent, Gen("Square"), new Color(0.02f, 0.05f, 0.15f, 0.6f));
            Stretch(dim.rectTransform);
            var card = UIImage("Card", dim.transform, UI("Panel"), Color.white);
            card.type = Image.Type.Sliced;
            Anchor(card.rectTransform, 0.5f, 0.5f, Vector2.zero, size);
            var tt = UIText("Title", card.transform, title, 64, TextAnchor.MiddleCenter, new Color(0.12f, 0.25f, 0.55f), 0.5f, 1, new Vector2(0, -70), new Vector2(size.x - 40, 100));
            tt.fontStyle = FontStyle.Bold;
            tt.GetComponent<Outline>().effectColor = new Color(1, 1, 1, 0.5f);
            if (title != "") Divider(card.transform, -128, size.x - 160); // the level-complete card puts its stars there
            box = card.transform;
            return dim.gameObject;
        }

        // =====================================================================
        // Main menu
        // =====================================================================
        static void BuildMainMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var theme = ThemeFor(0);

            var cam = MakeCamera(theme, new Vector3(0, 0, -10));
            BuildBackground(cam, theme, new Vector3(0, -2, 0));

            var lightGo = new GameObject("Global Light 2D");
            lightGo.AddComponent<Light2D>().lightType = Light2D.LightType.Global;

            // decorative slope
            var pts = new List<Vector2>();
            for (float x = -40; x <= 40; x += 0.5f) pts.Add(new Vector2(x, -4.5f - x * 0.12f + Mathf.Sin(x * 0.25f) * 0.6f));
            var terrainGo = new GameObject("Terrain");
            terrainGo.AddComponent<EdgeCollider2D>();
            terrainGo.AddComponent<Ground>();
            var terrain = terrainGo.AddComponent<TerrainGenerator>();
            Set(terrain, "fillMaterial", fillMat);
            Set(terrain, "edgeMaterial", edgeMat);
            SetColor(terrain, "bottomColor", theme.TerrainBottom);
            terrain.SetPoints(pts.ToArray());

            var rnd = new System.Random(5);
            for (float x = -30; x < 30; x += 5 + (float)rnd.NextDouble() * 8)
            {
                float s = 0.1f + (float)rnd.NextDouble() * 0.1f;
                var tr = SpriteObj("Tree", Lab(rnd.NextDouble() < 0.5 ? "Snow-Tree-1" : "Snow-Tree-2"), -6);
                tr.transform.localScale = new Vector3(s, s, 1);
                tr.transform.position = new Vector3(x, terrain.HeightAt(x) - 0.8f + 12f * s, 0);
                tr.GetComponent<SpriteRenderer>().color = new Color(0.85f, 0.9f, 1f);
            }

            // rider preview on the slope (right side of the screen)
            float px = 9.5f;
            var previewRoot = new GameObject("Rider Preview Root");
            previewRoot.transform.position = new Vector3(px, terrain.HeightAt(px) + 1.15f, 0);
            previewRoot.transform.rotation = Quaternion.Euler(0, 0, terrain.AngleAt(px));
            previewRoot.transform.localScale = Vector3.one * 1.3f;
            var preview = new GameObject("Rider Preview");
            preview.transform.SetParent(previewRoot.transform, false);
            var pBottom = SpriteObj("Boarder_Bottom", Lab("Boarder_Bottom"), 20, preview.transform);
            pBottom.transform.localPosition = new Vector3(0, -0.51f, 0);
            var pTop = SpriteObj("Boarder_Top", Lab("Boarder_Top"), 21, preview.transform);
            pTop.transform.localPosition = new Vector3(-0.21f, 0.1f, 0);

            var systems = new GameObject("Menu Systems");
            var audio = systems.AddComponent<AudioManager>();
            Set(audio, "crashClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Lab2Assets/Audio/Crash SFX.ogg"));
            Set(audio, "finishClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Lab2Assets/Audio/Finish SFX.ogg"));
            var weather = systems.AddComponent<WeatherController>();
            Set(weather, "snowMaterial", snowMat);
            SetFloat(weather, "baseSnowRate", 40f);
            SetFloat(weather, "gustSnowRate", 60f);
            SetFloat(weather, "gustFog", 0f);

            // ---- UI ----
            var canvas = MakeCanvas("Menu Canvas");
            var t = canvas.transform;
            var menu = canvas.gameObject.AddComponent<MainMenuController>();


            // main panel
            var main = Panel("Main Panel", t, false);
            var logo = UIImage("Logo", main, UI("Logo"), Color.white);
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            Anchor(logo.rectTransform, 0, 1, new Vector2(40, -10), new Vector2(1000, 275));
            var subtitle = UIText("Subtitle", main, "Shred the slopes · land flips · chain combos", 32, TextAnchor.UpperCenter, Color.white, 0, 1, new Vector2(40, -280), new Vector2(1000, 50));
            subtitle.fontStyle = FontStyle.Bold;
            subtitle.GetComponent<Outline>().effectColor = new Color(0.08f, 0.16f, 0.36f, 0.9f);
            var start = UIButton("Start", main, "START", Vector2.zero, new Vector2(460, 100), new Color(0.4f, 0.9f, 0.5f));
            var options = UIButton("Options", main, "OPTIONS", Vector2.zero, new Vector2(460, 100), Color.white);
            var help = UIButton("Help", main, "HOW TO PLAY", Vector2.zero, new Vector2(460, 100), Color.white);
            var quit = UIButton("Quit", main, "QUIT", Vector2.zero, new Vector2(460, 100), new Color(1f, 0.7f, 0.7f));
            var mainButtons = new[] { start, options, help, quit };
            var mainIcons = new[] { "Icon_Play", "Icon_Gear", "Icon_Help", "Icon_Quit" };
            for (int i = 0; i < mainButtons.Length; i++)
            {
                Anchor((RectTransform)mainButtons[i].transform, 0, 0.5f, new Vector2(310, 10 - i * 125), new Vector2(460, 104));
                ButtonIcon(mainButtons[i], UI(mainIcons[i]), 72);
            }

            // play setup panel
            var play = Panel("Play Panel", t, true);
            var playRt = (RectTransform)play;
            Anchor(playRt, 0, 0.5f, new Vector2(90, -70), new Vector2(1050, 860));
            UIText("Header", play, "RIDER SETUP", 52, TextAnchor.MiddleCenter, new Color(0.12f, 0.25f, 0.55f), 0.5f, 1, new Vector2(0, -55), new Vector2(900, 70)).fontStyle = FontStyle.Bold;
            Divider(play, -100, 800);

            var charPrev = UIButton("Char Prev", play, "<", new Vector2(-440, 0), new Vector2(80, 110), Color.white);
            var charNext = UIButton("Char Next", play, ">", new Vector2(440, 0), new Vector2(80, 110), Color.white);
            MoveTop(charPrev, -190); MoveTop(charNext, -190);
            ArrowButton(charPrev, false); ArrowButton(charNext, true);
            var charText = UIText("Character", play, "", 34, TextAnchor.MiddleCenter, new Color(0.12f, 0.2f, 0.4f), 0.5f, 1, new Vector2(0, -190), new Vector2(760, 130));
            charText.GetComponent<Outline>().enabled = false;

            var boardPrev = UIButton("Board Prev", play, "<", Vector2.zero, new Vector2(80, 70), Color.white);
            var boardNext = UIButton("Board Next", play, ">", Vector2.zero, new Vector2(80, 70), Color.white);
            MoveTop(boardPrev, -310); MoveTop(boardNext, -310);
            ((RectTransform)boardPrev.transform).anchoredPosition = new Vector2(-440, -310);
            ((RectTransform)boardNext.transform).anchoredPosition = new Vector2(440, -310);
            ArrowButton(boardPrev, false); ArrowButton(boardNext, true);
            var boardText = UIText("Board", play, "", 32, TextAnchor.MiddleCenter, new Color(0.12f, 0.2f, 0.4f), 0.5f, 1, new Vector2(0, -310), new Vector2(760, 70));
            boardText.GetComponent<Outline>().enabled = false;

            var modeBtn = UIButton("Mode", play, "", Vector2.zero, new Vector2(880, 100), new Color(0.75f, 0.88f, 1f));
            MoveTop(modeBtn, -425);
            var modeText = modeBtn.GetComponentInChildren<Text>();
            modeText.fontSize = 32;

            UIText("Mountain Header", play, "CHOOSE MOUNTAIN", 30, TextAnchor.MiddleCenter, new Color(0.12f, 0.25f, 0.55f), 0.5f, 1, new Vector2(0, -515), new Vector2(900, 40)).GetComponent<Outline>().enabled = false;
            var levelButtons = new List<Button>();
            var levelLabels = new List<Text>();
            for (int i = 0; i < LevelCatalog.Levels.Length; i++)
            {
                var b = UIButton("Level " + (i + 1), play, "", Vector2.zero, new Vector2(290, 120), Color.white);
                MoveTop(b, -610);
                ((RectTransform)b.transform).anchoredPosition = new Vector2((i - 1) * 300, -610);
                var lbl = b.GetComponentInChildren<Text>();
                lbl.fontSize = 28;
                levelButtons.Add(b); levelLabels.Add(lbl);
            }
            var go = UIButton("Go", play, "LET'S RIDE!", Vector2.zero, new Vector2(520, 100), new Color(0.4f, 0.9f, 0.5f));
            var back = UIButton("Back", play, "BACK", Vector2.zero, new Vector2(300, 100), Color.white);
            MoveTop(go, -770); MoveTop(back, -770);
            ((RectTransform)go.transform).anchoredPosition = new Vector2(-150, -770);
            ((RectTransform)back.transform).anchoredPosition = new Vector2(300, -770);

            // options panel
            var opt = Panel("Options Panel", t, true);
            Anchor((RectTransform)opt, 0, 0.5f, new Vector2(90, -70), new Vector2(900, 760));
            UIText("Header", opt, "OPTIONS", 52, TextAnchor.MiddleCenter, new Color(0.12f, 0.25f, 0.55f), 0.5f, 1, new Vector2(0, -55), new Vector2(800, 70)).fontStyle = FontStyle.Bold;
            Divider(opt, -100, 700);
            var master = UISlider("Master Volume", opt, "Master volume", -170);
            var sfx = UISlider("SFX Volume", opt, "Sound effects", -250);
            var music = UISlider("Music Volume", opt, "Background music", -330);
            var weatherT = UIToggle("Weather Toggle", opt, "Weather effects (snow & fog)", -420);
            var shakeT = UIToggle("Shake Toggle", opt, "Screen shake", -500);
            var reset = UIButton("Reset", opt, "RESET PROGRESS", Vector2.zero, new Vector2(420, 80), new Color(1f, 0.75f, 0.7f));
            var optBack = UIButton("Back", opt, "BACK", Vector2.zero, new Vector2(300, 90), Color.white);
            MoveTop(reset, -590); MoveTop(optBack, -680);

            // help panel
            var helpP = Panel("Help Panel", t, true);
            Anchor((RectTransform)helpP, 0, 0.5f, new Vector2(90, -70), new Vector2(1050, 860));
            UIText("Header", helpP, "HOW TO PLAY", 52, TextAnchor.MiddleCenter, new Color(0.12f, 0.25f, 0.55f), 0.5f, 1, new Vector2(0, -55), new Vector2(900, 70)).fontStyle = FontStyle.Bold;
            Divider(helpP, -100, 800);
            var helpText = UIText("Body", helpP,
                "<b>CONTROLS</b>\n" +
                "← / A, → / D : lean on the ground, spin in the air\n" +
                "↑ / W : push (accelerate)     ↓ / S : brake / carve\n" +
                "SPACE : jump     SHIFT (in air) : grab     ESC : pause     M : mute\n\n" +
                "<b>RULES</b>\n" +
                "• Land on your board – touching the snow with your head = crash.\n" +
                "• Spin 360° for flips, hold SHIFT for grabs, catch big air.\n" +
                "• Chain tricks & snowflakes before the combo bar empties to\n" +
                "   raise the multiplier (up to x5). Crashing or rocks break it.\n" +
                "• Rocks slow you down, pine trees make you crash.\n" +
                "• Power-ups: <color=#E07000>boost</color> · <color=#2070E0>shield</color> (smash obstacles) · <color=#E03060>extra life</color> ·\n" +
                "   <color=#8040E0>magnet</color> (pulls snowflakes) · <color=#20A050>clock</color> (-3s on the timer).\n" +
                "• Riding fast earns points too. Collect every snowflake for a bonus!",
                27, TextAnchor.UpperLeft, new Color(0.12f, 0.2f, 0.4f), 0.5f, 1, new Vector2(0, -110), new Vector2(940, 600));
            helpText.GetComponent<Outline>().enabled = false;
            var helpBack = UIButton("Back", helpP, "BACK", Vector2.zero, new Vector2(300, 90), Color.white);
            MoveTop(helpBack, -770);

            MakeEventSystem();

            Set(menu, "mainPanel", main.gameObject);
            Set(menu, "playPanel", play.gameObject);
            Set(menu, "optionsPanel", opt.gameObject);
            Set(menu, "helpPanel", helpP.gameObject);
            Set(menu, "startButton", start);
            Set(menu, "optionsButton", options);
            Set(menu, "helpButton", help);
            Set(menu, "quitButton", quit);
            Set(menu, "characterText", charText);
            Set(menu, "characterPrev", charPrev);
            Set(menu, "characterNext", charNext);
            Set(menu, "previewTop", pTop.GetComponent<SpriteRenderer>());
            Set(menu, "previewBottom", pBottom.GetComponent<SpriteRenderer>());
            Set(menu, "boardText", boardText);
            Set(menu, "boardPrev", boardPrev);
            Set(menu, "boardNext", boardNext);
            Set(menu, "modeText", modeText);
            Set(menu, "modeButton", modeBtn);
            SetArray(menu, "levelButtons", levelButtons.ToArray());
            SetArray(menu, "levelLabels", levelLabels.ToArray());
            Set(menu, "goButton", go);
            Set(menu, "playBackButton", back);
            Set(menu, "masterSlider", master);
            Set(menu, "sfxSlider", sfx);
            Set(menu, "musicSlider", music);
            Set(menu, "weatherToggle", weatherT);
            Set(menu, "shakeToggle", shakeT);
            Set(menu, "resetButton", reset);
            Set(menu, "optionsBackButton", optBack);
            Set(menu, "helpBackButton", helpBack);

            EditorSceneManager.SaveScene(scene, SceneDir + "MainMenu.unity");
        }

        static Transform Panel(string name, Transform parent, bool card)
        {
            if (!card)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                Stretch((RectTransform)go.transform);
                return go.transform;
            }
            var img = UIImage(name, parent, UI("Panel"), new Color(1f, 1f, 1f, 0.97f));
            img.type = Image.Type.Sliced;
            return img.transform;
        }

        static void MoveTop(Button b, float y)
        {
            var rt = (RectTransform)b.transform;
            var x = rt.anchoredPosition.x;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        // =====================================================================
        // UI helpers
        // =====================================================================
        static Canvas MakeCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static void MakeEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            // with no actions asset assigned the module falls back to the default UI actions at runtime
            es.AddComponent<InputSystemUIInputModule>();
        }

        static void Anchor(RectTransform rt, float ax, float ay, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image UIImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        static Text UIText(string name, Transform parent, string text, int size, TextAnchor align, Color color,
                           float ax, float ay, Vector2 pos, Vector2 rectSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.1f, 0.25f, 0.8f);
            o.effectDistance = new Vector2(2, -2);
            Anchor((RectTransform)go.transform, ax, ay, pos, rectSize);
            return t;
        }

        static Button UIButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, Color color)
        {
            var img = UIImage(name, parent, UI("Button"), color);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.5f; // keeps the 108px vertical border inside ~70-90px tall buttons
            Anchor(img.rectTransform, 0.5f, 0.5f, pos, size);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.93f, 1f);
            colors.selectedColor = new Color(0.85f, 0.93f, 1f);
            colors.pressedColor = new Color(0.7f, 0.8f, 0.95f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.65f, 0.6f);
            b.colors = colors;
            var t = UIText("Label", img.transform, label, 40, TextAnchor.MiddleCenter, new Color(0.1f, 0.18f, 0.38f), 0.5f, 0.5f, Vector2.zero, size);
            t.fontStyle = FontStyle.Bold;
            t.GetComponent<Outline>().enabled = false;
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(0, 8); // sit above the bevel
            return b;
        }

        /// <summary>Puts an icon on the left of a button and shifts its label right.</summary>
        static void ButtonIcon(Button b, Sprite icon, float size)
        {
            var img = UIImage("Icon", b.transform, icon, Color.white);
            img.raycastTarget = false;
            Anchor(img.rectTransform, 0, 0.5f, new Vector2(18, 4), new Vector2(size, size));
            var label = b.GetComponentInChildren<Text>().rectTransform;
            label.offsetMin = new Vector2(size + 10, label.offsetMin.y);
        }

        /// <summary>Replaces a "&lt;" / "&gt;" text button with a chevron icon.</summary>
        static void ArrowButton(Button b, bool right)
        {
            b.GetComponentInChildren<Text>().text = "";
            b.GetComponent<Image>().color = new Color(0.78f, 0.9f, 1f);
            var img = UIImage("Arrow", b.transform, UI(right ? "Icon_ArrowRight" : "Icon_ArrowLeft"), Color.white);
            img.raycastTarget = false;
            img.preserveAspect = true;
            Stretch(img.rectTransform);
            img.rectTransform.offsetMin = new Vector2(8, 16);
            img.rectTransform.offsetMax = new Vector2(-8, -8);
        }

        static void Divider(Transform parent, float y, float width)
        {
            var d = UIImage("Divider", parent, UI("Divider"), new Color(0.48f, 0.78f, 0.96f));
            d.raycastTarget = false;
            Anchor(d.rectTransform, 0.5f, 1, new Vector2(0, y), new Vector2(width, 16));
        }

        static Slider UISlider(string name, Transform parent, string label, float y)
        {
            UIText(name + " Label", parent, label, 32, TextAnchor.MiddleLeft, new Color(0.12f, 0.2f, 0.4f), 0.5f, 1, new Vector2(-170, y), new Vector2(440, 60))
                .GetComponent<Outline>().enabled = false;
            var res = new DefaultControls.Resources { standard = UI("PanelHUD"), background = UI("PanelHUD"), knob = Gen("SoftCircle") };
            var go = DefaultControls.CreateSlider(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Anchor(rt, 0.5f, 1, new Vector2(200, y), new Vector2(340, 34));
            var s = go.GetComponent<Slider>();
            s.minValue = 0; s.maxValue = 1;
            go.transform.Find("Background").GetComponent<Image>().color = new Color(0.75f, 0.82f, 0.92f);
            go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = new Color(0.35f, 0.7f, 1f);
            var handle = go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>();
            handle.color = new Color(0.12f, 0.3f, 0.7f);
            ((RectTransform)handle.transform).sizeDelta = new Vector2(44, 0);
            return s;
        }

        static Toggle UIToggle(string name, Transform parent, string label, float y)
        {
            var res = new DefaultControls.Resources { standard = UI("PanelHUD"), checkmark = Gen("Square") };
            var go = DefaultControls.CreateToggle(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, 0.5f, 1, new Vector2(0, y), new Vector2(680, 60));
            var bg = (RectTransform)go.transform.Find("Background");
            bg.anchorMin = bg.anchorMax = bg.pivot = new Vector2(0, 0.5f);
            bg.anchoredPosition = Vector2.zero; bg.sizeDelta = new Vector2(52, 52);
            bg.GetComponent<Image>().color = new Color(0.8f, 0.87f, 0.95f);
            var check = (RectTransform)bg.Find("Checkmark");
            check.anchorMin = Vector2.zero; check.anchorMax = Vector2.one; check.offsetMin = new Vector2(10, 10); check.offsetMax = new Vector2(-10, -10);
            check.GetComponent<Image>().color = new Color(0.2f, 0.55f, 1f);
            var lbl = go.transform.Find("Label").GetComponent<Text>();
            lbl.font = font; lbl.fontSize = 32; lbl.text = label; lbl.color = new Color(0.12f, 0.2f, 0.4f);
            var lrt = (RectTransform)lbl.transform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = new Vector2(72, 0); lrt.offsetMax = Vector2.zero;
            lbl.alignment = TextAnchor.MiddleLeft;
            return go.GetComponent<Toggle>();
        }
    }
}
