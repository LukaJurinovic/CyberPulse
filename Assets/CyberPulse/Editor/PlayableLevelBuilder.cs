using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AI;
using UnityEngine.Rendering;
using CyberPulse.Input;
using CyberPulse.Player;
using CyberPulse.UI;
using CyberPulse.Weapons;
using CyberPulse.Enemy;

namespace CyberPulse.Editor
{
    /// <summary>
    /// Builds the closed, fully playable test level in one click.
    /// Menu: CyberPulse ▶ Build Playable Level
    ///
    /// Creates Assets/Scenes/PlayableTestLevel.unity with:
    ///   - 60×60 enclosed arena (floor, 4 walls, ceiling)
    ///   - Jump platforms, wall-slide corridor, dash markers, cover blocks
    ///   - 4 enemies with patrol AI wired and ready to chase/attack
    ///   - Full player hierarchy (movement, dash, camera, weapon, debug UI)
    ///   - NavMesh baked for enemy pathfinding
    ///   - All serialized references wired — no Inspector work needed
    /// </summary>
    public static class PlayableLevelBuilder
    {
        private const string ScenePath       = "Assets/Scenes/PlayableTestLevel.unity";
        private const string InputReaderPath = "Assets/CyberPulse/Input/InputReader.asset";
        private const string GroundLayerName = "Ground";
        private const string PlayerLayerName = "Player";

        private static readonly Color CyanHDR    = new Color(0f,    0.961f, 1f,    1f);
        private static readonly Color CyanEmit   = new Color(0f,    2.4f,   2.5f,  1f);
        private static readonly Color DarkFloor  = new Color(0.04f, 0.04f,  0.08f, 1f);
        private static readonly Color WallColor  = new Color(0.06f, 0.07f,  0.12f, 1f);
        private static readonly Color PlatColor  = new Color(0.04f, 0.10f,  0.18f, 1f);
        private static readonly Color CoverColor = new Color(0.08f, 0.06f,  0.15f, 1f);
        private static readonly Color MarkerColor = new Color(0.12f, 0.05f, 0.18f, 1f);
        private static readonly Color EnemyColor = new Color(0.75f, 0.15f,  0.05f, 1f);

        // ──────────────────────────────────────────────────────────────────────
        // Entry point
        // ──────────────────────────────────────────────────────────────────────

        [MenuItem("CyberPulse/► Build Playable Level")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/CyberPulse/Input");

            int groundIdx  = EnsureLayer(GroundLayerName);
            int playerIdx  = EnsureLayer(PlayerLayerName);
            int groundMask = 1 << groundIdx;
            int playerMask = 1 << playerIdx;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.06f);
            RenderSettings.fog          = false;

            var inputReader = GetOrCreateInputReader();

            BuildArena(groundIdx);
            BuildInterior(groundIdx);
            BuildLighting();

            var player = BuildPlayer(inputReader, groundMask, playerIdx);
            BuildDebugUI(player);
            BuildEnemies(groundMask, playerMask, groundIdx);

            // Save before baking — Unity writes NavMesh.asset relative to the scene path.
            // Without a saved path the bake produces nothing and agents stand still.
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            BakeNavMesh();

            // Save again to commit the NavMesh asset reference into the scene.
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = player;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log("[CyberPulse] Playable level ready — press Play to test.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Arena — closed room 60×60, ceiling at y=9
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildArena(int groundLayerIdx)
        {
            var arena = new GameObject("Arena");

            // Floor — grid material
            var floor = MakeStaticBox(arena, "Floor",
                new Vector3(0f, -0.25f, 0f), new Vector3(60f, 0.5f, 60f), DarkFloor, groundLayerIdx);
            floor.GetComponent<MeshRenderer>().sharedMaterial = MakeGridMaterial();

            // Ceiling
            MakeStaticBox(arena, "Ceiling",
                new Vector3(0f, 9.25f, 0f), new Vector3(60f, 0.5f, 60f), WallColor, groundLayerIdx);

            // Walls (0.5 thick, seated flush against floor/ceiling)
            MakeStaticBox(arena, "Wall_North",
                new Vector3(0f,    4.5f,  30.25f), new Vector3(61f, 9f, 0.5f), WallColor, groundLayerIdx);
            MakeStaticBox(arena, "Wall_South",
                new Vector3(0f,    4.5f, -30.25f), new Vector3(61f, 9f, 0.5f), WallColor, groundLayerIdx);
            MakeStaticBox(arena, "Wall_East",
                new Vector3( 30.25f, 4.5f, 0f), new Vector3(0.5f, 9f, 61f), WallColor, groundLayerIdx);
            MakeStaticBox(arena, "Wall_West",
                new Vector3(-30.25f, 4.5f, 0f), new Vector3(0.5f, 9f, 61f), WallColor, groundLayerIdx);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Interior — gameplay elements
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildInterior(int groundLayerIdx)
        {
            var interior = new GameObject("Interior");

            // Jump platforms — northwest quadrant, x=-22..−6, z=+14
            float[] heights = { 1f, 2f, 3.5f, 5f, 7f };
            var platforms = new GameObject("JumpPlatforms");
            platforms.transform.SetParent(interior.transform, false);
            for (int i = 0; i < heights.Length; i++)
            {
                float px = -22f + i * 4f;
                MakeStaticBox(platforms, $"Plat_{i + 1}",
                    new Vector3(px, heights[i] + 0.1f, 14f),
                    new Vector3(3f, 0.2f, 3f), PlatColor, groundLayerIdx);
            }

            // Wall-slide corridor — east side, x=16 & x=19.5, runs z=−2..+12
            var corridor = new GameObject("WallSlide_Corridor");
            corridor.transform.SetParent(interior.transform, false);
            MakeStaticBox(corridor, "WallA",
                new Vector3(16f,   2.5f, 5f), new Vector3(0.4f, 5f, 14f), WallColor, groundLayerIdx);
            MakeStaticBox(corridor, "WallB",
                new Vector3(19.5f, 2.5f, 5f), new Vector3(0.4f, 5f, 14f), WallColor, groundLayerIdx);

            // Dash markers — along z from player spawn (0,0,−22) going north
            // Distance labels show meters from spawn; world z = −22 + d
            var markers = new GameObject("DashMarkers");
            markers.transform.SetParent(interior.transform, false);
            int[] distances = { 3, 5, 8, 12, 16, 20 };
            foreach (int d in distances)
            {
                float wz = -22f + d;
                var slab = MakeStaticBox(markers, $"Marker_{d}m",
                    new Vector3(0f, 1.5f, wz), new Vector3(2f, 3f, 0.15f), MarkerColor, groundLayerIdx);

                var label = new GameObject($"Label_{d}m");
                label.transform.SetParent(slab.transform, false);
                label.transform.localPosition = new Vector3(0f, 0.6f, -0.12f);
                var tmp = label.AddComponent<TMPro.TextMeshPro>();
                tmp.text = $"{d}m";
                tmp.fontSize = 2.5f;
                tmp.color = CyanHDR;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.rectTransform.sizeDelta = new Vector2(2f, 1f);
            }

            // Cover — scattered boxes to break sightlines and enable tactics
            var cover = new GameObject("Cover");
            cover.transform.SetParent(interior.transform, false);
            (Vector3 pos, Vector3 size)[] blocks =
            {
                (new Vector3(-9f,  1f,   2f), new Vector3(1f, 2f, 4f)),
                (new Vector3( 9f,  1f,   2f), new Vector3(1f, 2f, 4f)),
                (new Vector3(-9f,  1f,  -8f), new Vector3(4f, 2f, 1f)),
                (new Vector3( 9f,  1f,  -8f), new Vector3(4f, 2f, 1f)),
                (new Vector3( 0f,  1f,   8f), new Vector3(5f, 2f, 1f)),
                (new Vector3( 0f,  1f, -14f), new Vector3(3f, 2f, 1f)),
                // L-shape NW side
                (new Vector3(-14f, 1f,   0f), new Vector3(1f, 2f, 3f)),
                (new Vector3(-15f, 1f,  -1f), new Vector3(3f, 2f, 1f)),
                // L-shape NE side
                (new Vector3( 14f, 1f,   0f), new Vector3(1f, 2f, 3f)),
                (new Vector3( 15f, 1f,  -1f), new Vector3(3f, 2f, 1f)),
            };
            for (int i = 0; i < blocks.Length; i++)
                MakeStaticBox(cover, $"Cover_{i}", blocks[i].pos, blocks[i].size, CoverColor, groundLayerIdx);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Lighting — dark + neon cyan atmosphere
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildLighting()
        {
            var dirGO = new GameObject("Directional Light");
            dirGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var dir = dirGO.AddComponent<Light>();
            dir.type      = LightType.Directional;
            dir.intensity = 0.25f;
            dir.color     = new Color(0.85f, 0.85f, 1f);
            dir.shadows   = LightShadows.Soft;

            // Four cyan point lights inside the arena corners at mid-height
            float r = 24f;
            Vector3[] pts =
            {
                new Vector3(-r, 5f, -r), new Vector3(r, 5f, -r),
                new Vector3(-r, 5f,  r), new Vector3(r, 5f,  r),
            };
            for (int i = 0; i < pts.Length; i++)
            {
                var ptGO = new GameObject($"PointLight_Cyan_{i + 1}");
                ptGO.transform.position = pts[i];
                var pt = ptGO.AddComponent<Light>();
                pt.type      = LightType.Point;
                pt.color     = CyanHDR;
                pt.intensity = 4f;
                pt.range     = 20f;
                pt.shadows   = LightShadows.None;
            }

            // Extra warm fill light near player spawn
            var spawnLight = new GameObject("SpawnLight");
            spawnLight.transform.position = new Vector3(0f, 4f, -22f);
            var sl = spawnLight.AddComponent<Light>();
            sl.type      = LightType.Point;
            sl.color     = new Color(0.6f, 0.9f, 1f);
            sl.intensity = 2f;
            sl.range     = 12f;
            sl.shadows   = LightShadows.None;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Player hierarchy (south spawn, facing north)
        // ──────────────────────────────────────────────────────────────────────

        private static GameObject BuildPlayer(InputReader inputReader, int groundMask, int playerLayerIdx)
        {
            var player = new GameObject("Player");
            player.layer = playerLayerIdx;
            player.transform.position = new Vector3(0f, 1f, -22f);
            player.transform.rotation = Quaternion.identity;

            var rb = player.AddComponent<Rigidbody>();
            rb.mass                   = 1f;
            rb.useGravity             = true;
            rb.interpolation          = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints            = RigidbodyConstraints.FreezeRotation;

            var col = player.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.4f;
            col.center = new Vector3(0f, 0.9f, 0f);

            var controller = player.AddComponent<PlayerController>();
            var dash       = player.AddComponent<DashAbility>();
            player.AddComponent<PlayerStats>();

            // CameraPivot at eye height
            var pivotGO = new GameObject("CameraPivot");
            pivotGO.transform.SetParent(player.transform, false);
            pivotGO.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var camScript = pivotGO.AddComponent<PlayerCamera>();

            // Main camera
            var camGO = new GameObject("MainCamera");
            camGO.transform.SetParent(pivotGO.transform, false);
            camGO.transform.localPosition = Vector3.zero;
            camGO.tag = "MainCamera";

            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane  = 300f;
            cam.fieldOfView   = 75f;
            camGO.AddComponent<AudioListener>();
            TryEnablePostProcessing(camGO);

            // FX
            var fxRoot = new GameObject("FX");
            fxRoot.transform.SetParent(player.transform, false);
            var psGO = new GameObject("DashParticles");
            psGO.transform.SetParent(fxRoot.transform, false);
            var dashPs = psGO.AddComponent<ParticleSystem>();
            ConfigureDashParticles(dashPs);

            // Wire player scripts
            LinkComponent(controller, so =>
            {
                so.FindProperty("_input").objectReferenceValue           = inputReader;
                so.FindProperty("_cameraTransform").objectReferenceValue = pivotGO.transform;
                so.FindProperty("_groundLayer").intValue                 = groundMask;
            });
            LinkComponent(dash, so =>
            {
                so.FindProperty("_input").objectReferenceValue           = inputReader;
                so.FindProperty("_cameraTransform").objectReferenceValue = pivotGO.transform;
                so.FindProperty("_dashParticles").objectReferenceValue   = dashPs;
            });
            LinkComponent(camScript, so =>
            {
                so.FindProperty("_input").objectReferenceValue      = inputReader;
                so.FindProperty("_controller").objectReferenceValue = controller;
                so.FindProperty("_dash").objectReferenceValue       = dash;
                so.FindProperty("_camera").objectReferenceValue     = cam;
            });

            // Weapon socket attached to camera
            var socketGO = new GameObject("WeaponSocket");
            socketGO.transform.SetParent(camGO.transform, false);
            socketGO.transform.localPosition = new Vector3(0.18f, -0.22f, 0.35f);

            var rifleGO = new GameObject("Weapon_AssaultRifle");
            rifleGO.transform.SetParent(socketGO.transform, false);

            var hitscan     = rifleGO.AddComponent<HitscanWeapon>();
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
                so.FindProperty("_input").objectReferenceValue      = inputReader;
                so.FindProperty("_controller").objectReferenceValue = controller;
            });

            var holder = player.AddComponent<WeaponHolder>();
            LinkComponent(holder, so =>
            {
                so.FindProperty("_input").objectReferenceValue           = inputReader;
                so.FindProperty("_cameraTransform").objectReferenceValue = pivotGO.transform;
                var arr = so.FindProperty("_weapons");
                arr.arraySize = 1;
                arr.GetArrayElementAtIndex(0).objectReferenceValue = hitscan;
            });

            return player;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Debug UI
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildDebugUI(GameObject player)
        {
            var go = new GameObject("DebugUI");
            var ui = go.AddComponent<MovementDebugUI>();
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
        // Enemies — 4 placed around the arena with patrol circuits
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildEnemies(int groundMask, int playerMask, int groundLayerIdx)
        {
            var root = new GameObject("Enemies");

            // Northwest — patrols near jump platforms
            CreateEnemy("Enemy_NW", root, new Vector3(-20f, 0f, 20f), groundMask, playerMask,
                new[]
                {
                    new Vector3(-22f, 0f, 22f), new Vector3(-10f, 0f, 22f),
                    new Vector3(-10f, 0f, 10f), new Vector3(-22f, 0f, 10f),
                });

            // Northeast — patrols open east zone
            CreateEnemy("Enemy_NE", root, new Vector3(8f, 0f, 20f), groundMask, playerMask,
                new[]
                {
                    new Vector3(10f, 0f, 24f), new Vector3( 5f, 0f, 24f),
                    new Vector3( 5f, 0f, 12f), new Vector3(10f, 0f, 12f),
                });

            // West side — patrols cover area
            CreateEnemy("Enemy_W", root, new Vector3(-20f, 0f, -5f), groundMask, playerMask,
                new[]
                {
                    new Vector3(-22f, 0f, -3f), new Vector3(-12f, 0f, -3f),
                    new Vector3(-12f, 0f, -15f), new Vector3(-22f, 0f, -15f),
                });

            // Center — most aggressive, patrols across the middle
            CreateEnemy("Enemy_C", root, new Vector3(5f, 0f, 4f), groundMask, playerMask,
                new[]
                {
                    new Vector3( 8f, 0f,  8f), new Vector3(-8f, 0f,  8f),
                    new Vector3(-8f, 0f, -4f), new Vector3( 8f, 0f, -4f),
                });
        }

        private static void CreateEnemy(string name, GameObject parent, Vector3 position,
            int groundMask, int playerMask, Vector3[] patrolPositions)
        {
            var enemy = new GameObject(name);
            enemy.transform.SetParent(parent.transform, false);
            enemy.transform.position = position;

            // Collider (root)
            var col = enemy.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.4f;
            col.center = new Vector3(0f, 1f, 0f);

            // NavMeshAgent
            var agent = enemy.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.height          = 2f;
            agent.radius          = 0.4f;
            agent.angularSpeed    = 360f;
            agent.acceleration    = 12f;
            agent.stoppingDistance = 0.5f;

            // Visual — capsule mesh without its own collider
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(enemy.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
            visual.GetComponent<MeshRenderer>().sharedMaterial = MakeSolidMaterial(EnemyColor);

            // Hit VFX
            var hitFXGO = new GameObject("HitVFX");
            hitFXGO.transform.SetParent(enemy.transform, false);
            var hitPs = hitFXGO.AddComponent<ParticleSystem>();
            ConfigureHitVFX(hitPs);

            // Patrol point objects
            var patrolParent = new GameObject("PatrolPoints");
            patrolParent.transform.SetParent(enemy.transform, false);
            var patrolTransforms = new Transform[patrolPositions.Length];
            for (int i = 0; i < patrolPositions.Length; i++)
            {
                var pt = new GameObject($"Point_{i}");
                pt.transform.SetParent(patrolParent.transform, false);
                pt.transform.position = patrolPositions[i];
                patrolTransforms[i] = pt.transform;
            }

            // Script components — order matters: dependencies before EnemyController
            var health     = enemy.AddComponent<EnemyHealth>();
            var sensor     = enemy.AddComponent<EnemySensor>();
            var attack     = enemy.AddComponent<EnemyAttack>();
            var controller = enemy.AddComponent<EnemyController>();

            // Wire serialized fields
            LinkComponent(health, so =>
                so.FindProperty("_hitVFX").objectReferenceValue = hitPs);

            LinkComponent(sensor, so =>
            {
                so.FindProperty("_detectionRange").floatValue   = 20f;
                so.FindProperty("_fieldOfView").floatValue      = 160f;
                so.FindProperty("_checkInterval").floatValue    = 0.15f;
                so.FindProperty("_playerLayer").intValue        = playerMask;
                so.FindProperty("_obstructionLayer").intValue   = groundMask;
            });

            LinkComponent(attack, so =>
            {
                so.FindProperty("_damage").intValue             = 10;
                so.FindProperty("_cooldown").floatValue         = 1.5f;
                so.FindProperty("_range").floatValue            = 2.5f;
                so.FindProperty("_playerLayer").intValue        = playerMask;
            });

            LinkComponent(controller, so =>
            {
                var pts = so.FindProperty("_patrolPoints");
                pts.arraySize = patrolTransforms.Length;
                for (int i = 0; i < patrolTransforms.Length; i++)
                    pts.GetArrayElementAtIndex(i).objectReferenceValue = patrolTransforms[i];
            });
        }

        // ──────────────────────────────────────────────────────────────────────
        // NavMesh baking
        // ──────────────────────────────────────────────────────────────────────

        private static void BakeNavMesh()
        {
            try
            {
                NavMeshBuilder.BuildNavMesh();
                Debug.Log("[CyberPulse] NavMesh baked successfully.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CyberPulse] NavMesh auto-bake failed: " + e.Message +
                    " — open Window > AI > Navigation and click Bake manually.");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Particle systems
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
            emission.enabled      = true;
            emission.rateOverTime = 0f;
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

            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = MakeParticleMaterial(CyanHDR);
        }

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
            var orange = new Color(1f, 0.85f, 0.3f);
            grad.SetKeys(
                new[] { new GradientColorKey(orange, 0f), new GradientColorKey(new Color(1f, 0.4f, 0f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = MakeParticleMaterial(orange);
        }

        private static void ConfigureHitVFX(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration      = 0.1f;
            main.loop          = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize     = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor    = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0f));
            main.maxParticles  = 30;
            main.stopAction    = ParticleSystemStopAction.Disable;
            main.playOnAwake   = false;

            var emission = ps.emission;
            emission.enabled      = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.1f;

            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = MakeParticleMaterial(new Color(1f, 0.3f, 0f));
        }

        // ──────────────────────────────────────────────────────────────────────
        // Material helpers
        // ──────────────────────────────────────────────────────────────────────

        private static Material MakeGridMaterial()
        {
            var mat = new Material(UrpLit()) { name = "M_Grid" };
            var tex  = MakeGridTex(512, 32, DarkFloor, new Color(0f, 0.96f, 1f, 0.4f));
            var emit = MakeGridTex(512, 32, Color.black, CyanEmit * 0.3f);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_EmissionMap", emit);
            mat.SetColor("_EmissionColor", CyanEmit * 0.3f);
            mat.EnableKeyword("_EMISSION");
            mat.SetTextureScale("_BaseMap",     new Vector2(60f, 60f));
            mat.SetTextureScale("_EmissionMap", new Vector2(60f, 60f));
            return mat;
        }

        private static Material MakeSolidMaterial(Color color)
        {
            var mat = new Material(UrpLit());
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        private static Material MakeParticleMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Standard Unlit");
            var mat = shader != null ? new Material(shader) : new Material(UrpLit());
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        private static Shader UrpLit() =>
            Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        private static Texture2D MakeGridTex(int size, int cell, Color bg, Color line)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
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
        // Primitive helpers
        // ──────────────────────────────────────────────────────────────────────

        private static GameObject MakeStaticBox(GameObject parent, string name,
            Vector3 localPos, Vector3 size, Color color, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name  = name;
            go.layer = layer;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = MakeSolidMaterial(color);
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.NavigationStatic |
                StaticEditorFlags.ContributeGI     |
                StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Layer & folder utilities
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

            Debug.LogError($"[CyberPulse] No empty layer slot for \"{name}\". Create it manually in Project Settings > Tags & Layers.");
            return 0;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Serialized-field wiring helper
        // ──────────────────────────────────────────────────────────────────────

        private static void LinkComponent<T>(T component, Action<SerializedObject> assign)
            where T : UnityEngine.Object
        {
            var so = new SerializedObject(component);
            assign(so);
            so.ApplyModifiedProperties();
        }

        // ──────────────────────────────────────────────────────────────────────
        // InputReader ScriptableObject
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
        // URP post-processing — via reflection to avoid hard package dependency
        // ──────────────────────────────────────────────────────────────────────

        private static void TryEnablePostProcessing(GameObject cameraGO)
        {
            var urpType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, " +
                "Unity.RenderPipelines.Universal.Runtime");
            if (urpType == null) return;

            var existing = cameraGO.GetComponent(urpType);
            var data = existing != null ? existing : cameraGO.AddComponent(urpType);
            urpType.GetProperty("renderPostProcessing")?.SetValue(data, true);
        }
    }
}
