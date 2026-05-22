using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using CyberPulse.Input;
using CyberPulse.Player;
using CyberPulse.UI;
using CyberPulse.Weapons;

namespace CyberPulse.Editor
{
    /// <summary>
    /// One-click scene bootstrapper.
    /// Menu: CyberPulse ▶ Bootstrap Full Playable Scene
    ///
    /// What it does:
    ///   1. Creates the "Ground" layer if missing.
    ///   2. Creates (or reuses) the InputReader ScriptableObject asset.
    ///   3. Builds a playable environment (ground, jump platforms, wall corridor).
    ///   4. Constructs the full Player hierarchy with every component configured.
    ///   5. Wires all serialized references — no manual Inspector work needed.
    ///   6. Saves Assets/Scenes/MovementTestScene.unity.
    /// </summary>
    public static class FullSceneBootstrapper
    {
        private const string ScenePath       = "Assets/Scenes/MovementTestScene.unity";
        private const string InputReaderPath = "Assets/CyberPulse/Input/InputReader.asset";
        private const string GroundLayerName = "Ground";

        private static readonly Color CyanHDR   = new Color(0f,    0.961f, 1f,   1f);
        private static readonly Color CyanEmit  = new Color(0f,    2.4f,   2.5f, 1f);
        private static readonly Color DarkFloor = new Color(0.04f, 0.04f,  0.08f, 1f);
        private static readonly Color WallCol   = new Color(0.08f, 0.08f,  0.15f, 1f);
        private static readonly Color PlatCol   = new Color(0.05f, 0.12f,  0.20f, 1f);

        // ──────────────────────────────────────────────────────────────────────
        // Entry point
        // ──────────────────────────────────────────────────────────────────────

        [MenuItem("CyberPulse/► Bootstrap Full Playable Scene")]
        public static void Bootstrap()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/CyberPulse/Input");

            int groundIdx  = EnsureLayer(GroundLayerName);
            int groundMask = 1 << groundIdx;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.06f);
            RenderSettings.fog          = false;

            var inputReader = GetOrCreateInputReader();

            BuildEnvironment(groundIdx);
            BuildLighting();
            var player = BuildPlayer(inputReader, groundMask);
            BuildDebugUI(player);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = player;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log("[CyberPulse] Scene ready — press Play to test movement.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Layer setup
        // ──────────────────────────────────────────────────────────────────────

        private static int EnsureLayer(string name)
        {
            int idx = LayerMask.NameToLayer(name);
            if (idx >= 0) return idx;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
            var layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                var elem = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(elem.stringValue)) continue;

                elem.stringValue = name;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[CyberPulse] Layer \"{name}\" created at index {i}.");
                return i;
            }

            Debug.LogError($"[CyberPulse] No empty layer slot. Create \"{name}\" manually in Project Settings.");
            return 0;
        }

        // ──────────────────────────────────────────────────────────────────────
        // InputReader asset
        // ──────────────────────────────────────────────────────────────────────

        private static InputReader GetOrCreateInputReader()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InputReader>(InputReaderPath);
            if (existing != null) return existing;

            var reader = ScriptableObject.CreateInstance<InputReader>();
            AssetDatabase.CreateAsset(reader, InputReaderPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CyberPulse] InputReader asset created at {InputReaderPath}.");
            return reader;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Environment
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildEnvironment(int groundLayerIdx)
        {
            var env = new GameObject("Environment");

            // Ground — 40×40 grid plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name  = "Ground";
            ground.layer = groundLayerIdx;
            ground.transform.SetParent(env.transform, false);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = MakeGridMaterial();

            // Wall-slide corridor at X = 10
            var corridor = new GameObject("WallSlide_Corridor");
            corridor.transform.SetParent(env.transform, false);
            corridor.transform.position = new Vector3(10f, 0f, 0f);

            MakeBox(corridor, "WallA", new Vector3( 1.75f, 2.5f, 0f), new Vector3(0.25f, 5f, 14f), WallCol, groundLayerIdx);
            MakeBox(corridor, "WallB", new Vector3(-1.75f, 2.5f, 0f), new Vector3(0.25f, 5f, 14f), WallCol, groundLayerIdx);

            // Jump platforms — increasing heights, spaced on -X axis
            var platforms = new GameObject("JumpPlatforms");
            platforms.transform.SetParent(env.transform, false);
            platforms.transform.position = new Vector3(-8f, 0f, 4f);

            float[] heights = { 1f, 2f, 3.5f, 5f, 7f };
            for (int i = 0; i < heights.Length; i++)
                MakeBox(platforms, $"Plat_{i + 1}",
                    new Vector3(i * 4f, heights[i] + 0.1f, 0f),
                    new Vector3(3f, 0.2f, 3f), PlatCol, groundLayerIdx);

            // Dash markers — thin slabs along +Z, labelled with distance
            var markers = new GameObject("DashMarkers");
            markers.transform.SetParent(env.transform, false);

            int[] distances = { 3, 5, 8, 12, 16, 20 };
            foreach (int d in distances)
            {
                var slab = MakeBox(markers, $"Marker_{d}m",
                    new Vector3(0f, 1.5f, d), new Vector3(2f, 3f, 0.1f),
                    new Color(0.12f, 0.05f, 0.18f), groundLayerIdx);

                // TMP label
                var label = new GameObject($"Label_{d}m");
                label.transform.SetParent(slab.transform, false);
                label.transform.localPosition = new Vector3(0f, 0.5f, -0.1f);
                var tmp = label.AddComponent<TMPro.TextMeshPro>();
                tmp.text = $"{d}m";
                tmp.fontSize = 3f;
                tmp.color = CyanHDR;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.rectTransform.sizeDelta = new Vector2(2f, 1f);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Lighting
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildLighting()
        {
            var dirGO = new GameObject("Directional Light");
            dirGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var dir = dirGO.AddComponent<Light>();
            dir.type      = LightType.Directional;
            dir.intensity = 0.3f;
            dir.color     = new Color(0.85f, 0.85f, 1f);
            dir.shadows   = LightShadows.Soft;

            float r = 18f;
            Vector3[] pts = {
                new Vector3(-r, 4f, -r), new Vector3(r, 4f, -r),
                new Vector3(-r, 4f,  r), new Vector3(r, 4f,  r),
            };
            for (int i = 0; i < pts.Length; i++)
            {
                var ptGO = new GameObject($"PointLight_Cyan_{i + 1}");
                ptGO.transform.position = pts[i];
                var pt = ptGO.AddComponent<Light>();
                pt.type      = LightType.Point;
                pt.color     = CyanHDR;
                pt.intensity = 3f;
                pt.range     = 12f;
                pt.shadows   = LightShadows.None;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Player hierarchy
        // ──────────────────────────────────────────────────────────────────────

        private static GameObject BuildPlayer(InputReader inputReader, int groundMask)
        {
            // ── Root ──────────────────────────────────────────────────────────
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, 0f);

            // Rigidbody
            var rb = player.AddComponent<Rigidbody>();
            rb.mass                  = 1f;
            rb.useGravity            = true;
            rb.interpolation         = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints           = RigidbodyConstraints.FreezeRotation;
            // drag is 0 at start — PlayerController sets it based on ground state

            // CapsuleCollider
            var col = player.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.4f;
            col.center = new Vector3(0f, 0.9f, 0f);

            // Script components
            var controller = player.AddComponent<PlayerController>();
            var dash       = player.AddComponent<DashAbility>();
            player.AddComponent<PlayerStats>();

            // ── CameraPivot ───────────────────────────────────────────────────
            var pivotGO = new GameObject("CameraPivot");
            pivotGO.transform.SetParent(player.transform, false);
            pivotGO.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var camScript = pivotGO.AddComponent<PlayerCamera>();

            // ── MainCamera ────────────────────────────────────────────────────
            var camGO = new GameObject("MainCamera");
            camGO.transform.SetParent(pivotGO.transform, false);
            camGO.transform.localPosition = Vector3.zero;
            camGO.tag = "MainCamera";

            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane  = 300f;
            cam.fieldOfView   = 75f;
            camGO.AddComponent<AudioListener>();

            // Enable URP post-processing via reflection (avoids hard package dep)
            TryEnablePostProcessing(camGO);

            // ── FX / DashParticles ────────────────────────────────────────────
            var fxRoot = new GameObject("FX");
            fxRoot.transform.SetParent(player.transform, false);

            var psGO = new GameObject("DashParticles");
            psGO.transform.SetParent(fxRoot.transform, false);
            var ps = psGO.AddComponent<ParticleSystem>();
            ConfigureDashParticles(ps);

            // ── Wire all SerializedFields ─────────────────────────────────────
            LinkComponent(controller, so =>
            {
                so.FindProperty("_input").objectReferenceValue            = inputReader;
                so.FindProperty("_cameraTransform").objectReferenceValue  = pivotGO.transform;
                so.FindProperty("_groundLayer").intValue                  = groundMask;
            });

            LinkComponent(dash, so =>
            {
                so.FindProperty("_input").objectReferenceValue            = inputReader;
                so.FindProperty("_cameraTransform").objectReferenceValue  = pivotGO.transform;
                so.FindProperty("_dashParticles").objectReferenceValue    = ps;
            });

            LinkComponent(camScript, so =>
            {
                so.FindProperty("_input").objectReferenceValue       = inputReader;
                so.FindProperty("_controller").objectReferenceValue  = controller;
                so.FindProperty("_dash").objectReferenceValue        = dash;
                so.FindProperty("_camera").objectReferenceValue      = cam;
            });

            // ── WeaponSocket (child of MainCamera) ────────────────────────────
            var socketGO = new GameObject("WeaponSocket");
            socketGO.transform.SetParent(camGO.transform, false);
            socketGO.transform.localPosition = new Vector3(0.18f, -0.22f, 0.35f);

            // Default weapon: Assault Rifle (HitscanWeapon)
            var rifleGO = new GameObject("Weapon_AssaultRifle");
            rifleGO.transform.SetParent(socketGO.transform, false);

            var hitscan    = rifleGO.AddComponent<HitscanWeapon>();
            var weaponAudio = rifleGO.AddComponent<AudioSource>();
            weaponAudio.spatialBlend = 0f;

            var muzzleFlashGO = new GameObject("MuzzleFlash");
            muzzleFlashGO.transform.SetParent(rifleGO.transform, false);
            muzzleFlashGO.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            var muzzlePs = muzzleFlashGO.AddComponent<ParticleSystem>();
            ConfigureMuzzleFlash(muzzlePs);

            var sway = rifleGO.AddComponent<WeaponSway>();

            LinkComponent(hitscan, so =>
            {
                so.FindProperty("_muzzleFlash").objectReferenceValue  = muzzlePs;
                so.FindProperty("_audioSource").objectReferenceValue  = weaponAudio;
            });

            LinkComponent(sway, so =>
            {
                so.FindProperty("_input").objectReferenceValue       = inputReader;
                so.FindProperty("_controller").objectReferenceValue  = controller;
            });

            // WeaponHolder on player root
            var holder = player.AddComponent<WeaponHolder>();
            LinkComponent(holder, so =>
            {
                so.FindProperty("_input").objectReferenceValue           = inputReader;
                so.FindProperty("_cameraTransform").objectReferenceValue = pivotGO.transform;
                var weaponsProp = so.FindProperty("_weapons");
                weaponsProp.arraySize = 1;
                weaponsProp.GetArrayElementAtIndex(0).objectReferenceValue = hitscan;
            });

            return player;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Debug UI object
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildDebugUI(GameObject player)
        {
            var go        = new GameObject("DebugUI");
            var ui        = go.AddComponent<MovementDebugUI>();
            var crosshair = go.AddComponent<CrosshairUI>();

            LinkComponent(ui, so =>
            {
                so.FindProperty("_controller").objectReferenceValue = player.GetComponent<PlayerController>();
                so.FindProperty("_dash").objectReferenceValue       = player.GetComponent<DashAbility>();
            });

            LinkComponent(crosshair, so =>
            {
                so.FindProperty("_controller").objectReferenceValue   = player.GetComponent<PlayerController>();
                so.FindProperty("_weaponHolder").objectReferenceValue = player.GetComponent<WeaponHolder>();
            });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Particle system configuration
        // ──────────────────────────────────────────────────────────────────────

        private static void ConfigureDashParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration      = 0.3f;
            main.loop          = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(8f);
            main.startSize     = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor    = new ParticleSystem.MinMaxGradient(CyanHDR);
            main.maxParticles  = 60;
            main.stopAction    = ParticleSystemStopAction.Disable;

            var emission = ps.emission;
            emission.enabled       = true;
            emission.rateOverTime  = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = 25f;
            shape.radius    = 0.1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(CyanHDR, 0f), new GradientColorKey(CyanHDR, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode    = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = MakeParticleMaterial();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Material helpers
        // ──────────────────────────────────────────────────────────────────────

        private static Material MakeGridMaterial()
        {
            var mat = new Material(UrpLit());
            mat.name = "M_Grid";
            var tex = MakeGridTex(512, 32, DarkFloor, new Color(0f, 0.96f, 1f, 0.4f));
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_EmissionMap", MakeGridTex(512, 32, Color.black, CyanEmit * 0.35f));
            mat.SetColor("_EmissionColor", CyanEmit * 0.35f);
            mat.EnableKeyword("_EMISSION");
            mat.SetTextureScale("_BaseMap",    new Vector2(40f, 40f));
            mat.SetTextureScale("_EmissionMap", new Vector2(40f, 40f));
            return mat;
        }

        private static Material MakeParticleMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Standard Unlit");
            if (shader == null) return new Material(UrpLit());
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", CyanHDR);
            return mat;
        }

        private static Material MakeSolidMaterial(Color color)
        {
            var mat = new Material(UrpLit());
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        private static Shader UrpLit() =>
            Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        private static Texture2D MakeGridTex(int size, int cell, Color bg, Color line)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = (x % cell == 0 || y % cell == 0) ? line : bg;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Primitive helper
        // ──────────────────────────────────────────────────────────────────────

        private static GameObject MakeBox(GameObject parent, string name,
            Vector3 localPos, Vector3 size, Color color, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name  = name;
            go.layer = layer;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = MakeSolidMaterial(color);
            return go;
        }

        // ──────────────────────────────────────────────────────────────────────
        // SerializedObject helper — sets fields then applies
        // ──────────────────────────────────────────────────────────────────────

        private static void LinkComponent<T>(T component, Action<SerializedObject> assign)
            where T : UnityEngine.Object
        {
            var so = new SerializedObject(component);
            assign(so);
            so.ApplyModifiedProperties();
        }

        // ──────────────────────────────────────────────────────────────────────
        // URP post-processing via reflection (avoids hard package dependency)
        // ──────────────────────────────────────────────────────────────────────

        private static void TryEnablePostProcessing(GameObject cameraGO)
        {
            var urpDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, " +
                "Unity.RenderPipelines.Universal.Runtime");

            if (urpDataType == null) return;

            var existing = cameraGO.GetComponent(urpDataType);
            var data = existing != null
                ? existing
                : cameraGO.AddComponent(urpDataType);

            var prop = urpDataType.GetProperty("renderPostProcessing");
            prop?.SetValue(data, true);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Muzzle flash particle configuration
        // ──────────────────────────────────────────────────────────────────────

        private static void ConfigureMuzzleFlash(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration      = 0.05f;
            main.loop          = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSize     = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor    = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f));
            main.maxParticles  = 20;
            main.stopAction    = ParticleSystemStopAction.Disable;

            var emission = ps.emission;
            emission.enabled      = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = 20f;
            shape.radius    = 0.02f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode    = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = MakeParticleMaterial();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Folder utilities
        // ──────────────────────────────────────────────────────────────────────

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
        }
    }
}
