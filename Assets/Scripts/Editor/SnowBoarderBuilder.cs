using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SnowBoarder.EditorTools
{
    /// <summary>
    /// One-click builder: Tools > Snow Boarder > Build Everything.
    /// Imports/configures sprites, creates materials + prefabs and generates the
    /// MainMenu and Level1..3 scenes, then registers them in Build Settings.
    /// </summary>
    public static partial class SnowBoarderBuilder
    {
        const string Root = "Assets";
        const string LabSprites = "Assets/Lab2Assets/Sprites/";
        const string GenSprites = "Assets/Art/Generated/";
        const string MatDir = "Assets/Art/Materials/";
        const string PrefabDir = "Assets/Prefabs/";
        const string SceneDir = "Assets/Scenes/";

        static Font font;
        static Material fillMat, edgeMat, particleMat, snowMat;
        static PhysicsMaterial2D snowPhysics, playerPhysics;
        static GameObject snowflakePrefab, rockPrefab, rock2Prefab, treePrefab, boostPrefab, checkpointPrefab, sparklePrefab, playerPrefab;
        static readonly Dictionary<PowerUpType, GameObject> powerUpPrefabs = new Dictionary<PowerUpType, GameObject>();

        [MenuItem("Tools/Snow Boarder/Build Everything")]
        public static void BuildEverything()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ConfigureTextures();
            CreateMaterials();
            CreatePrefabs();
            BuildMainMenu();
            for (int i = 0; i < LevelCatalog.Levels.Length; i++) BuildLevel(i);

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(SceneDir + "MainMenu.unity", true) };
            foreach (var l in LevelCatalog.Levels) scenes.Add(new EditorBuildSettingsScene(SceneDir + l.SceneName + ".unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
            PlayerSettings.productName = "Snow Boarder";
            PlayerSettings.runInBackground = true;

            EditorSceneManager.OpenScene(SceneDir + "MainMenu.unity");
            Debug.Log("[SnowBoarder] Build complete: MainMenu + " + LevelCatalog.Levels.Length + " levels.");
        }

        // =====================================================================
        // Assets
        // =====================================================================
        static void ConfigureTextures()
        {
            EnsureFolder(MatDir); EnsureFolder(PrefabDir); EnsureFolder(SceneDir);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Generated" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                bool changed = imp.textureType != TextureImporterType.Sprite;
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                imp.spritePixelsPerUnit = 100;
                imp.wrapMode = file == "SnowEdge" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                bool ui = path.StartsWith(GenSprites + "UI/");
                if (file == "Panel") imp.spriteBorder = ui ? new Vector4(64, 64, 64, 64) : new Vector4(30, 30, 30, 30);
                if (file == "Button") imp.spriteBorder = ui ? new Vector4(46, 60, 46, 48) : new Vector4(24, 24, 24, 24);
                if (file == "PanelHUD") imp.spriteBorder = new Vector4(44, 44, 44, 44);
                if (file == "Logo") imp.mipmapEnabled = true;
                if (file == "Checkpoint") imp.spritePivot = new Vector2(0.1f, 0f);
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                if (file == "Checkpoint") { settings.spriteAlignment = (int)SpriteAlignment.Custom; imp.SetTextureSettings(settings); }
                imp.SaveAndReimport();
            }
            // lab fill texture must repeat for the terrain body
            var fillImp = (TextureImporter)AssetImporter.GetAtPath(LabSprites + "blue-fill.png");
            fillImp.wrapMode = TextureWrapMode.Repeat;
            fillImp.SaveAndReimport();
            AssetDatabase.Refresh();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static void CreateMaterials()
        {
            var spriteUnlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            fillMat = MakeMaterial("TerrainFill", spriteUnlit, Tex(LabSprites + "blue-fill.png"));
            edgeMat = MakeMaterial("SnowEdge", spriteUnlit, Tex(GenSprites + "SnowEdge.png"));
            particleMat = MakeMaterial("SoftParticle", spriteUnlit, Tex(GenSprites + "SoftCircle.png"));
            snowMat = MakeMaterial("Snowfall", spriteUnlit, Tex(GenSprites + "SoftCircle.png"));

            snowPhysics = LoadOrCreate(MatDir + "Snow.physicsMaterial2D", () => new PhysicsMaterial2D("Snow"));
            snowPhysics.friction = 0.04f; snowPhysics.bounciness = 0f;
            playerPhysics = LoadOrCreate(MatDir + "Board.physicsMaterial2D", () => new PhysicsMaterial2D("Board"));
            playerPhysics.friction = 0.04f; playerPhysics.bounciness = 0f;
            EditorUtility.SetDirty(snowPhysics); EditorUtility.SetDirty(playerPhysics);
            AssetDatabase.SaveAssets();
        }

        static Material MakeMaterial(string name, Shader shader, Texture tex)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.mainTexture = tex;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static T LoadOrCreate<T>(string path, System.Func<T> create) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a) return a;
            a = create();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        static Sprite Spr(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
        static Sprite Lab(string name) => Spr(LabSprites + name + ".png");
        static Sprite Gen(string name) => Spr(GenSprites + name + ".png");
        static Sprite UI(string name) => Spr(GenSprites + "UI/" + name + ".png");
        static Sprite BG(string name) => Spr(GenSprites + "BG/" + name + ".png");

        static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        // =====================================================================
        // Prefabs
        // =====================================================================
        static void CreatePrefabs()
        {
            sparklePrefab = SavePrefab(MakeSparkle(), "Sparkle");

            // snowflake
            var sf = SpriteObj("Snowflake", Gen("Snowflake"), 6);
            sf.transform.localScale = Vector3.one * 0.6f;
            var sfc = sf.AddComponent<CircleCollider2D>(); sfc.isTrigger = true; sfc.radius = 0.75f;
            var col = sf.AddComponent<Collectible>();
            Set(col, "collectEffectPrefab", sparklePrefab);
            snowflakePrefab = SavePrefab(sf, "Snowflake");

            // power-ups
            var defs = new (PowerUpType type, string sprite, float duration)[]
            {
                (PowerUpType.SpeedBoost, "PU_Speed", 4f),
                (PowerUpType.Invincibility, "PU_Shield", 7f),
                (PowerUpType.ExtraLife, "PU_Life", 0f),
                (PowerUpType.Magnet, "PU_Magnet", 9f),
                (PowerUpType.TimeBonus, "PU_Clock", 0f),
            };
            powerUpPrefabs.Clear();
            foreach (var d in defs)
            {
                var go = SpriteObj("PowerUp_" + d.type, Gen(d.sprite), 7);
                go.transform.localScale = Vector3.one * 0.9f;
                var c = go.AddComponent<CircleCollider2D>(); c.isTrigger = true; c.radius = 0.6f;
                var pu = go.AddComponent<PowerUp>();
                SetEnum(pu, "type", (int)d.type);
                SetFloat(pu, "duration", d.duration);
                Set(pu, "collectEffectPrefab", sparklePrefab);
                powerUpPrefabs[d.type] = SavePrefab(go, "PowerUp_" + d.type);
            }

            // rocks (slow you down)
            rockPrefab = MakeObstacle("Rock", Lab("Snow-Rock"), ObstacleKind.Rock, 0.55f, new Vector2(1.9f, 0.9f), new Vector2(0, -0.1f));
            rock2Prefab = MakeObstacle("Rock2", Lab("Snow-Rock-2"), ObstacleKind.Rock, 0.5f, new Vector2(1.9f, 1.2f), new Vector2(0, -0.15f));
            // small pine (crash!)
            treePrefab = MakeObstacle("Tree", Lab("Snow-Tree-1"), ObstacleKind.Tree, 0.085f, new Vector2(7f, 16f), new Vector2(0, -5f));

            // boost pad
            var bp = SpriteObj("BoostPad", Gen("BoostPad"), 2);
            var bpc = bp.AddComponent<BoxCollider2D>(); bpc.isTrigger = true; bpc.size = new Vector2(1.9f, 1.2f); bpc.offset = new Vector2(0, 0.4f);
            bp.AddComponent<BoostPad>();
            boostPrefab = SavePrefab(bp, "BoostPad");

            // checkpoint flag
            var cp = new GameObject("Checkpoint");
            var flag = SpriteObj("Flag", Gen("Checkpoint"), 2);
            flag.transform.SetParent(cp.transform, false);
            flag.transform.localScale = Vector3.one * 1.6f;
            var cpc = cp.AddComponent<BoxCollider2D>(); cpc.isTrigger = true; cpc.size = new Vector2(2f, 30f);
            var cpt = cp.AddComponent<Checkpoint>();
            Set(cpt, "flag", flag.GetComponent<SpriteRenderer>());
            checkpointPrefab = SavePrefab(cp, "Checkpoint");

            playerPrefab = SavePrefab(MakePlayer(), "Player");
            AssetDatabase.SaveAssets();
        }

        static GameObject MakeObstacle(string name, Sprite sprite, ObstacleKind kind, float scale, Vector2 box, Vector2 offset)
        {
            var go = SpriteObj(name, sprite, 5);
            go.transform.localScale = Vector3.one * scale;
            var c = go.AddComponent<BoxCollider2D>(); c.isTrigger = true; c.size = box; c.offset = offset;
            var o = go.AddComponent<Obstacle>();
            SetEnum(o, "kind", (int)kind);
            Set(o, "smashEffectPrefab", sparklePrefab);
            return SavePrefab(go, name);
        }

        static GameObject MakePlayer()
        {
            var root = new GameObject("Player");
            var rb = root.AddComponent<Rigidbody2D>();
            rb.mass = 1f;
            rb.gravityScale = 2f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.sharedMaterial = playerPhysics;

            var board = root.AddComponent<CapsuleCollider2D>();
            board.direction = CapsuleDirection2D.Horizontal;
            board.size = new Vector2(2.7f, 0.3f);
            board.offset = new Vector2(0f, -0.98f);
            board.sharedMaterial = playerPhysics;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var bottom = SpriteObj("Boarder_Bottom", Lab("Boarder_Bottom"), 20);
            bottom.transform.SetParent(visual.transform, false);
            bottom.transform.localPosition = new Vector3(0f, -0.51f, 0f);
            var top = SpriteObj("Boarder_Top", Lab("Boarder_Top"), 21);
            top.transform.SetParent(visual.transform, false);
            top.transform.localPosition = new Vector3(-0.21f, 0.1f, 0f);

            var shield = SpriteObj("Shield", Gen("SoftCircle"), 22);
            shield.transform.SetParent(visual.transform, false);
            shield.transform.localScale = Vector3.one * 5.2f;
            shield.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            shield.GetComponent<SpriteRenderer>().color = new Color(0.4f, 0.75f, 1f, 0.55f);

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0.35f, 0.59f, 0f);
            var hc = head.AddComponent<CircleCollider2D>(); hc.isTrigger = false; hc.radius = 0.3f;
            var ph = head.AddComponent<PlayerHead>();

            // snow spray behind the board
            var spray = MakeParticles("Snow Spray", root.transform, new Vector3(-1.2f, -1.05f, 0f), 19);
            {
                var m = spray.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
                m.startColor = new Color(1f, 1f, 1f, 0.9f);
                m.gravityModifier = 0.6f;
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                var sh = spray.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f; sh.radius = 0.1f;
                sh.rotation = new Vector3(-30f, -90f, 0f); // emit backwards and up
                var em = spray.emission; em.rateOverTime = 0f;
                var sz = spray.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            }

            // crash burst
            var crash = MakeParticles("Crash Effect", root.transform, new Vector3(0.3f, 0.4f, 0f), 23);
            {
                var m = crash.main;
                m.loop = false; m.playOnAwake = false; m.duration = 0.5f;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                m.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.7f, 0.9f, 1f));
                m.gravityModifier = 1.2f;
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                var sh = crash.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.4f;
                var em = crash.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
                var sz = crash.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            }

            // boost trail
            var trailGo = new GameObject("Boost Trail");
            trailGo.transform.SetParent(root.transform, false);
            trailGo.transform.localPosition = new Vector3(-0.8f, -0.6f, 0f);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.widthCurve = AnimationCurve.Linear(0, 1.2f, 1, 0f);
            trail.sharedMaterial = particleMat;
            trail.colorGradient = Grad(new Color(1f, 0.75f, 0.2f, 0.9f), new Color(1f, 0.3f, 0.1f, 0f));
            trail.sortingOrder = 18;
            trail.emitting = false;

            var pc = root.AddComponent<PlayerController>();
            Set(pc, "topRenderer", top.GetComponent<SpriteRenderer>());
            Set(pc, "bottomRenderer", bottom.GetComponent<SpriteRenderer>());
            Set(pc, "shieldVisual", shield);
            Set(pc, "snowSpray", spray);
            Set(pc, "crashEffect", crash);
            Set(pc, "boostTrail", trail);
            Set(ph, "player", pc);

            var anim = root.AddComponent<BoarderAnimator>();
            Set(anim, "player", pc);
            Set(anim, "visualRoot", visual.transform);
            Set(anim, "top", top.transform);
            return root;
        }

        static GameObject MakeSparkle()
        {
            var go = new GameObject("Sparkle");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = false; m.duration = 0.3f; m.playOnAwake = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 0.7f), new Color(0.6f, 0.9f, 1f));
            m.gravityModifier = 0.3f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.2f;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = particleMat; r.sortingOrder = 30;
            return go;
        }

        static ParticleSystem MakeParticles(string name, Transform parent, Vector3 pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = particleMat;
            r.sortingOrder = order;
            return ps;
        }

        static Gradient Grad(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                      new[] { new GradientAlphaKey(a.a, 0), new GradientAlphaKey(b.a, 1) });
            return g;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject SpriteObj(string name, Sprite sprite, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return go;
        }

        static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float rotZ = 0f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, 0, rotZ);
            return go;
        }

        // =====================================================================
        // Serialized field helpers
        // =====================================================================
        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"Field {field} not found on {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float v)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string field, int v)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).intValue = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetEnum(Object target, string field, int v)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).enumValueIndex = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetColor(Object target, string field, Color c)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).colorValue = c;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
