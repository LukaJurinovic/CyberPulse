using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CyberPulse.Enemy;
using CyberPulse.Input;
using CyberPulse.Player;
using CyberPulse.Systems;
using CyberPulse.UI;
using CyberPulse.Weapons;
using CyberPulse.World;

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
            BuildSystems(inputReader, player, groundMask, playerMask, groundIdx);

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

            // Ceiling + Walls use emissive material so AudioReactor can pulse them
            var wallMat = MakeWallEmissiveMaterial();

            var ceiling = MakeStaticBox(arena, "Ceiling",
                new Vector3(0f, 9.25f, 0f), new Vector3(60f, 0.5f, 60f), WallColor, groundLayerIdx);
            ceiling.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Walls (0.5 thick, seated flush against floor/ceiling)
            var wallN = MakeStaticBox(arena, "Wall_North",
                new Vector3(0f,    4.5f,  30.25f), new Vector3(61f, 9f, 0.5f), WallColor, groundLayerIdx);
            wallN.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            var wallS = MakeStaticBox(arena, "Wall_South",
                new Vector3(0f,    4.5f, -30.25f), new Vector3(61f, 9f, 0.5f), WallColor, groundLayerIdx);
            wallS.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            var wallE = MakeStaticBox(arena, "Wall_East",
                new Vector3( 30.25f, 4.5f, 0f), new Vector3(0.5f, 9f, 61f), WallColor, groundLayerIdx);
            wallE.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            var wallW = MakeStaticBox(arena, "Wall_West",
                new Vector3(-30.25f, 4.5f, 0f), new Vector3(0.5f, 9f, 61f), WallColor, groundLayerIdx);
            wallW.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Interior — gameplay elements
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildInterior(int groundLayerIdx)
        {
            var interior = new GameObject("Interior");

            // All obstacles are procedurally generated at runtime by ProceduralArenaGenerator.
            var generator = interior.AddComponent<ProceduralArenaGenerator>();
            LinkComponent(generator, so =>
                so.FindProperty("_groundLayerIndex").intValue = groundLayerIdx);
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

            var controller  = player.AddComponent<PlayerController>();
            var dash        = player.AddComponent<DashAbility>();
            var playerStats = player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerDeathHandler>();

            // Trace Meter is the sole health mechanic — effective HP death requires 9999/10 = ~1000 hits.
            LinkComponent(playerStats, so =>
                so.FindProperty("_maxHealth").intValue = 9999);

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
                so.FindProperty("_weaponName").stringValue            = "Assault Rifle";
                so.FindProperty("_specialCost").floatValue            = 60f;
                so.FindProperty("_muzzleFlash").objectReferenceValue  = muzzlePs;
                so.FindProperty("_audioSource").objectReferenceValue  = weaponAudio;
            });
            LinkComponent(sway, so =>
            {
                so.FindProperty("_input").objectReferenceValue      = inputReader;
                so.FindProperty("_controller").objectReferenceValue = controller;
            });

            // SFX clips — loaded once, shared below.
            var sfxRevolverShot   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/revolver_shot.mp3");
            var sfxRevolverReload = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/revolver_reload.mp3");
            var sfxShotgunShot    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/shotgun_shot.mp3");
            var sfxShotgunReload  = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/shotgun_reload.mp3");

            if (sfxRevolverShot   == null) Debug.LogWarning("[CyberPulse] Assets/Sfx/revolver_shot.mp3 not found.");
            if (sfxRevolverReload == null) Debug.LogWarning("[CyberPulse] Assets/Sfx/revolver_reload.mp3 not found.");
            if (sfxShotgunShot    == null) Debug.LogWarning("[CyberPulse] Assets/Sfx/shotgun_shot.mp3 not found.");
            if (sfxShotgunReload  == null) Debug.LogWarning("[CyberPulse] Assets/Sfx/shotgun_reload.mp3 not found.");

            // Revolver — high-damage, slow fire rate, 6-round cylinder
            var revolverGO = new GameObject("Weapon_Revolver");
            revolverGO.transform.SetParent(socketGO.transform, false);
            revolverGO.SetActive(false);
            var revolver      = revolverGO.AddComponent<RevolverWeapon>();
            var revolverAudio = revolverGO.AddComponent<AudioSource>();
            revolverAudio.spatialBlend = 0f;
            var revolverFlashGO = new GameObject("MuzzleFlash");
            revolverFlashGO.transform.SetParent(revolverGO.transform, false);
            revolverFlashGO.transform.localPosition = new Vector3(0f, 0f, 0.4f);
            var revolverFlashPs = revolverFlashGO.AddComponent<ParticleSystem>();
            ConfigureMuzzleFlash(revolverFlashPs);
            LinkComponent(revolver, so =>
            {
                so.FindProperty("_weaponName").stringValue            = "Revolver";
                so.FindProperty("_specialCost").floatValue            = 50f;
                so.FindProperty("_isAutomatic").boolValue             = false;
                so.FindProperty("_fireRate").floatValue               = 1.5f;
                so.FindProperty("_magazineSize").intValue             = 6;
                so.FindProperty("_reserveAmmo").intValue              = 30;
                so.FindProperty("_reloadDuration").floatValue         = 1.2f;
                so.FindProperty("_muzzleFlash").objectReferenceValue  = revolverFlashPs;
                so.FindProperty("_audioSource").objectReferenceValue  = revolverAudio;
                if (sfxRevolverShot   != null)
                    so.FindProperty("_fireClip").objectReferenceValue   = sfxRevolverShot;
                if (sfxRevolverReload != null)
                    so.FindProperty("_reloadClip").objectReferenceValue = sfxRevolverReload;
            });

            // Shotgun — 8-pellet pump, 2-round mag
            var shotgunGO = new GameObject("Weapon_Shotgun");
            shotgunGO.transform.SetParent(socketGO.transform, false);
            shotgunGO.SetActive(false);
            var shotgun      = shotgunGO.AddComponent<ShotgunWeapon>();
            var shotgunAudio = shotgunGO.AddComponent<AudioSource>();
            shotgunAudio.spatialBlend = 0f;
            var shotgunFlashGO = new GameObject("MuzzleFlash");
            shotgunFlashGO.transform.SetParent(shotgunGO.transform, false);
            shotgunFlashGO.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            var shotgunFlashPs = shotgunFlashGO.AddComponent<ParticleSystem>();
            ConfigureMuzzleFlash(shotgunFlashPs);
            LinkComponent(shotgun, so =>
            {
                so.FindProperty("_weaponName").stringValue            = "Shotgun";
                so.FindProperty("_specialCost").floatValue            = 70f;
                so.FindProperty("_isAutomatic").boolValue             = false;
                so.FindProperty("_fireRate").floatValue               = 1.0f;
                so.FindProperty("_magazineSize").intValue             = 2;
                so.FindProperty("_reserveAmmo").intValue              = 16;
                so.FindProperty("_reloadDuration").floatValue         = 2.0f;
                so.FindProperty("_muzzleFlash").objectReferenceValue  = shotgunFlashPs;
                so.FindProperty("_audioSource").objectReferenceValue  = shotgunAudio;
                if (sfxShotgunShot   != null)
                    so.FindProperty("_fireClip").objectReferenceValue   = sfxShotgunShot;
                if (sfxShotgunReload != null)
                    so.FindProperty("_reloadClip").objectReferenceValue = sfxShotgunReload;
            });

            var holder = player.AddComponent<WeaponHolder>();
            LinkComponent(holder, so =>
            {
                so.FindProperty("_input").objectReferenceValue           = inputReader;
                so.FindProperty("_cameraTransform").objectReferenceValue = pivotGO.transform;
                var arr = so.FindProperty("_weapons");
                arr.arraySize = 3;
                arr.GetArrayElementAtIndex(0).objectReferenceValue = hitscan;
                arr.GetArrayElementAtIndex(1).objectReferenceValue = revolver;
                arr.GetArrayElementAtIndex(2).objectReferenceValue = shotgun;
            });

            // Grid floor proximity glow — feeds player world pos to GridFloor.shader each frame
            player.AddComponent<GridFloorUpdater>();

            return player;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Debug UI
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildDebugUI(GameObject player)
        {
            var go = new GameObject("DebugUI");
            var ui        = go.AddComponent<MovementDebugUI>();
            var crosshair = go.AddComponent<CrosshairUI>();
            var hud       = go.AddComponent<DiegeticHUD>();

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
            // Diegetic world-space HUD — replaces the old OnGUI GameHUD
            var cameraPivot = player.GetComponentInChildren<PlayerCamera>().transform;
            LinkComponent(hud, so =>
            {
                so.FindProperty("_playerStats").objectReferenceValue  = player.GetComponent<PlayerStats>();
                so.FindProperty("_weaponHolder").objectReferenceValue = player.GetComponent<WeaponHolder>();
                so.FindProperty("_cameraPivot").objectReferenceValue  = cameraPivot;
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
            visual.GetComponent<MeshRenderer>().sharedMaterial = MakeWireframeMaterial();

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
            var shards     = enemy.AddComponent<EnemyDeathShards>();
            var sensor     = enemy.AddComponent<EnemySensor>();
            var attack     = enemy.AddComponent<EnemyAttack>();
            var controller = enemy.AddComponent<EnemyController>();

            // Wire serialized fields
            LinkComponent(health, so =>
            {
                so.FindProperty("_hitVFX").objectReferenceValue      = hitPs;
                so.FindProperty("_deathShards").objectReferenceValue = shards;
            });

            LinkComponent(shards, so =>
                so.FindProperty("_enemyRenderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());

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
        // Game systems
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildSystems(InputReader inputReader, GameObject player,
            int groundMask, int playerMask, int groundLayerIdx)
        {
            var go = new GameObject("GameSystems");
            go.AddComponent<GameManager>();
            var traceMeter = go.AddComponent<TraceMeter>();
            go.AddComponent<PhaseVisuals>();
            go.AddComponent<ScoreManager>();
            go.AddComponent<AudioAnalyzer>();
            var songAnalyzer = go.AddComponent<SongAnalyzer>();
            var beatClock    = go.AddComponent<BeatClock>();
            go.AddComponent<SyncGauge>();

            // ── Dynamic music (ambient + action stems crossfade at 50% trace) ──
            // With a single stem both sources use the same clip; DynamicMusicPlayer
            // syncs their playback positions to prevent phasing artefacts.
            // Swap in a second "action" clip on _actionSrc once you have the asset.
            AudioSource ambSrc = null;   // captured for BeatClock / WaveDirector wiring below
            var musicGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Music" });
            AudioClip ambientClip = null, actionClip = null;
            foreach (var guid in musicGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                string fn = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (fn.Contains("action") || fn.Contains("combat") || fn.Contains("intense"))
                    actionClip  = clip;
                else if (ambientClip == null)
                    ambientClip = clip;
            }
            // Single-clip fallback — both channels share the clip until a second stem exists.
            if (ambientClip == null && actionClip != null) { ambientClip = actionClip; }
            if (actionClip  == null && ambientClip != null)  actionClip  = ambientClip;

            if (ambientClip != null)
            {
                ambSrc             = go.AddComponent<AudioSource>();
                ambSrc.clip        = ambientClip;
                ambSrc.playOnAwake = true;
                ambSrc.loop        = false;   // play once — WaveDirector detects end for win state
                ambSrc.spatialBlend = 0f;
                ambSrc.volume      = 1f;

                var actSrc         = go.AddComponent<AudioSource>();
                actSrc.clip        = actionClip;
                actSrc.playOnAwake = false;   // DynamicMusicPlayer starts it in sync
                actSrc.loop        = false;
                actSrc.spatialBlend = 0f;
                actSrc.volume      = 0f;

                var dynMusic = go.AddComponent<DynamicMusicPlayer>();
                LinkComponent(dynMusic, so =>
                {
                    so.FindProperty("_ambientSrc").objectReferenceValue = ambSrc;
                    so.FindProperty("_actionSrc").objectReferenceValue  = actSrc;
                });
                Debug.Log($"[CyberPulse] Dynamic music wired: ambient={ambientClip.name}, action={actionClip.name}");

                // Wire the same ambient source to BeatClock (beat timing) and SongAnalyzer (BPM detection).
                LinkComponent(beatClock, so =>
                    so.FindProperty("_musicSource").objectReferenceValue = ambSrc);
                LinkComponent(songAnalyzer, so =>
                    so.FindProperty("_musicSource").objectReferenceValue = ambSrc);
            }

            var envReactor = go.AddComponent<EnvironmentAudioReactor>();
            var analyzer   = go.GetComponent<AudioAnalyzer>();
            LinkComponent(envReactor, so =>
                so.FindProperty("_analyzer").objectReferenceValue = analyzer);

            var tm = go.AddComponent<TimeManager>();
            LinkComponent(tm, so =>
                so.FindProperty("_input").objectReferenceValue = inputReader);

            // Damage post-processing volume — chromatic aberration spike on hit
            var damageVol   = BuildDamageVolume();
            // Critical volume — tightened vignette + red tint at 80% trace
            var critVol     = BuildCriticalVolume();
            var playerCamera = player.GetComponentInChildren<PlayerCamera>();
            var playerStats  = player.GetComponent<PlayerStats>();

            var ppCtrl = go.AddComponent<PostProcessingController>();
            LinkComponent(ppCtrl, so =>
            {
                so.FindProperty("_damageVolume").objectReferenceValue  = damageVol;
                so.FindProperty("_playerStats").objectReferenceValue   = playerStats;
                so.FindProperty("_playerCamera").objectReferenceValue  = playerCamera;
            });

            // Wire TraceMeter's critical volume, player stats, and music source (for time-scaling fill)
            LinkComponent(traceMeter, so =>
            {
                so.FindProperty("_criticalVolume").objectReferenceValue = critVol;
                so.FindProperty("_playerStats").objectReferenceValue    = playerStats;
                if (ambSrc != null)
                    so.FindProperty("_musicSource").objectReferenceValue = ambSrc;
            });

            // 1000+ instanced data-bits floating around the arena
            var dataBits = go.AddComponent<DataBitRenderer>();
            LinkComponent(dataBits, so =>
                so.FindProperty("_player").objectReferenceValue = player.transform);

            // Glitch renderer feature + controller
            var glitchFeature = EnsureGlitchRendererFeature();
            var glitchCtrl    = go.AddComponent<GlitchController>();
            LinkComponent(glitchCtrl, so =>
            {
                so.FindProperty("_playerStats").objectReferenceValue = playerStats;
                if (glitchFeature != null)
                    so.FindProperty("_feature").objectReferenceValue = glitchFeature;
            });

            // ── Rhythm systems ─────────────────────────────────────────────────
            // WaveDirector: song-driven enemy wave spawner.
            var waveDirector   = go.AddComponent<WaveDirector>();
            var seekerPrefab   = CreateSeekerPrefab(groundMask, playerMask, groundLayerIdx);
            var spherePrefab   = CreateSpherePrefab(groundMask, playerMask, groundLayerIdx);
            var trianglePrefab = CreateTrianglePrefab(groundMask, playerMask, groundLayerIdx);
            var cylinderPrefab = CreateCylinderPrefab(groundMask, playerMask, groundLayerIdx);
            var cubePrefab     = CreateCubePrefab(groundMask, playerMask, groundLayerIdx);
            LinkComponent(waveDirector, so =>
            {
                if (ambSrc != null)
                    so.FindProperty("_musicSource").objectReferenceValue = ambSrc;
                if (seekerPrefab   != null) so.FindProperty("_seekerPrefab").objectReferenceValue   = seekerPrefab;
                if (spherePrefab   != null) so.FindProperty("_spherePrefab").objectReferenceValue   = spherePrefab;
                if (trianglePrefab != null) so.FindProperty("_trianglePrefab").objectReferenceValue = trianglePrefab;
                if (cylinderPrefab != null) so.FindProperty("_cylinderPrefab").objectReferenceValue = cylinderPrefab;
                if (cubePrefab     != null) so.FindProperty("_cubePrefab").objectReferenceValue     = cubePrefab;
            });

            // BeatReactor: links player state to BeatClock (damage bonuses, SYNC, penalties).
            var dash        = player.GetComponent<DashAbility>();
            var controller  = player.GetComponent<PlayerController>();
            var beatReactor = go.AddComponent<BeatReactor>();
            LinkComponent(beatReactor, so =>
            {
                so.FindProperty("_controller").objectReferenceValue  = controller;
                so.FindProperty("_dash").objectReferenceValue        = dash;
                so.FindProperty("_playerStats").objectReferenceValue = playerStats;
            });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Seeker prefab — used by WaveDirector for procedural wave spawning
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates (or reuses) a Seeker enemy prefab at Assets/CyberPulse/Prefabs/Seeker.prefab.
        /// The prefab has no patrol points so spawned seekers idle in place until they
        /// detect the player via EnemySensor, then chase and attack.
        /// </summary>
        private static GameObject CreateSeekerPrefab(int groundMask, int playerMask, int groundLayerIdx)
        {
            const string prefabPath = "Assets/CyberPulse/Prefabs/Seeker.prefab";
            EnsureFolder("Assets/CyberPulse/Prefabs");

            // Reuse an existing prefab so rebuilding the scene doesn't duplicate assets.
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

            // Build a temporary scene object to extract to a prefab.
            var root = new GameObject("Seeker_Temp");

            var col = root.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.4f;
            col.center = new Vector3(0f, 1f, 0f);

            var agent           = root.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.height        = 2f;
            agent.radius        = 0.4f;
            agent.angularSpeed  = 360f;
            agent.acceleration  = 12f;
            agent.stoppingDistance = 0.5f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
            visual.GetComponent<MeshRenderer>().sharedMaterial = MakeWireframeMaterial();

            var hitFXGO = new GameObject("HitVFX");
            hitFXGO.transform.SetParent(root.transform, false);
            var hitPs = hitFXGO.AddComponent<ParticleSystem>();
            ConfigureHitVFX(hitPs);

            var health     = root.AddComponent<EnemyHealth>();
            var shards     = root.AddComponent<EnemyDeathShards>();
            var sensor     = root.AddComponent<EnemySensor>();
            var attack     = root.AddComponent<EnemyAttack>();
            root.AddComponent<EnemyController>();   // no patrol points → idles until detected

            LinkComponent(health, so =>
            {
                so.FindProperty("_hitVFX").objectReferenceValue      = hitPs;
                so.FindProperty("_deathShards").objectReferenceValue = shards;
            });
            LinkComponent(shards, so =>
                so.FindProperty("_enemyRenderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());
            LinkComponent(sensor, so =>
            {
                so.FindProperty("_detectionRange").floatValue  = 20f;
                so.FindProperty("_fieldOfView").floatValue     = 160f;
                so.FindProperty("_checkInterval").floatValue   = 0.15f;
                so.FindProperty("_playerLayer").intValue       = playerMask;
                so.FindProperty("_obstructionLayer").intValue  = groundMask;
            });
            LinkComponent(attack, so =>
            {
                so.FindProperty("_damage").intValue      = 10;
                so.FindProperty("_cooldown").floatValue  = 1.5f;
                so.FindProperty("_range").floatValue     = 2.5f;
                so.FindProperty("_playerLayer").intValue = playerMask;
            });

            // Save as prefab, then clean up the temp object.
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CyberPulse] Seeker prefab created at {prefabPath}.");
            return prefab;
        }

        // ──────────────────────────────────────────────────────────────────────
        // P2 Enemy prefabs
        // ──────────────────────────────────────────────────────────────────────

        private static GameObject CreateSpherePrefab(int groundMask, int playerMask, int groundLayerIdx)
        {
            const string path = "Assets/CyberPulse/Prefabs/Sphere.prefab";
            EnsureFolder("Assets/CyberPulse/Prefabs");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("Sphere_Temp");

            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.5f; col.center = Vector3.zero;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<SphereCollider>());
            visual.GetComponent<MeshRenderer>().sharedMaterial = MakeWireframeMaterial();

            var hitFXGO = new GameObject("HitVFX");
            hitFXGO.transform.SetParent(root.transform, false);
            var hitPs = hitFXGO.AddComponent<ParticleSystem>();
            ConfigureHitVFX(hitPs);

            var health  = root.AddComponent<EnemyHealth>();
            var shards  = root.AddComponent<EnemyDeathShards>();
            var sensor  = root.AddComponent<EnemySensor>();
            var aerial  = root.AddComponent<EnemySphereAerial>();

            LinkComponent(health, so =>
            {
                so.FindProperty("_maxHealth").intValue               = 80;
                so.FindProperty("_hitVFX").objectReferenceValue      = hitPs;
                so.FindProperty("_deathShards").objectReferenceValue = shards;
            });
            LinkComponent(shards, so =>
                so.FindProperty("_enemyRenderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());
            LinkComponent(sensor, so =>
            {
                so.FindProperty("_detectionRange").floatValue  = 25f;
                so.FindProperty("_fieldOfView").floatValue     = 360f;
                so.FindProperty("_checkInterval").floatValue   = 0.2f;
                so.FindProperty("_playerLayer").intValue       = playerMask;
                so.FindProperty("_obstructionLayer").intValue  = groundMask;
            });
            LinkComponent(aerial, so =>
                so.FindProperty("_playerLayer").intValue = playerMask);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CyberPulse] Sphere prefab created at {path}.");
            return prefab;
        }

        private static GameObject CreateTrianglePrefab(int groundMask, int playerMask, int groundLayerIdx)
        {
            const string path = "Assets/CyberPulse/Prefabs/Triangle.prefab";
            EnsureFolder("Assets/CyberPulse/Prefabs");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("Triangle_Temp");

            var col = root.AddComponent<CapsuleCollider>();
            col.height = 1.6f; col.radius = 0.4f; col.center = new Vector3(0, 0.8f, 0);

            // Visual — cube rotated 45° to look diamond-like
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            visual.transform.localScale    = new Vector3(0.7f, 0.7f, 0.7f);
            visual.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            visual.GetComponent<MeshRenderer>().sharedMaterial = MakeWireframeMaterial();

            var hitFXGO = new GameObject("HitVFX");
            hitFXGO.transform.SetParent(root.transform, false);
            var hitPs = hitFXGO.AddComponent<ParticleSystem>();
            ConfigureHitVFX(hitPs);

            var health   = root.AddComponent<EnemyHealth>();
            var shards   = root.AddComponent<EnemyDeathShards>();
            var sensor   = root.AddComponent<EnemySensor>();
            var triangle = root.AddComponent<EnemyTriangleMirror>();

            LinkComponent(health, so =>
            {
                so.FindProperty("_maxHealth").intValue               = 60;
                so.FindProperty("_hitVFX").objectReferenceValue      = hitPs;
                so.FindProperty("_deathShards").objectReferenceValue = shards;
            });
            LinkComponent(shards, so =>
                so.FindProperty("_enemyRenderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());
            LinkComponent(sensor, so =>
            {
                so.FindProperty("_detectionRange").floatValue  = 20f;
                so.FindProperty("_fieldOfView").floatValue     = 160f;
                so.FindProperty("_checkInterval").floatValue   = 0.15f;
                so.FindProperty("_playerLayer").intValue       = playerMask;
                so.FindProperty("_obstructionLayer").intValue  = groundMask;
            });
            LinkComponent(triangle, so =>
                so.FindProperty("_playerLayer").intValue = playerMask);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CyberPulse] Triangle prefab created at {path}.");
            return prefab;
        }

        private static GameObject CreateCylinderPrefab(int groundMask, int playerMask, int groundLayerIdx)
        {
            const string path = "Assets/CyberPulse/Prefabs/Cylinder.prefab";
            EnsureFolder("Assets/CyberPulse/Prefabs");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("Cylinder_Temp");

            var col = root.AddComponent<CapsuleCollider>();
            col.height = 2f; col.radius = 0.4f; col.center = new Vector3(0, 1f, 0);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localScale    = new Vector3(0.8f, 1f, 0.8f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
            visual.GetComponent<MeshRenderer>().sharedMaterial = MakeWireframeMaterial();

            var hitFXGO = new GameObject("HitVFX");
            hitFXGO.transform.SetParent(root.transform, false);
            var hitPs = hitFXGO.AddComponent<ParticleSystem>();
            ConfigureHitVFX(hitPs);

            var health   = root.AddComponent<EnemyHealth>();
            var shards   = root.AddComponent<EnemyDeathShards>();
            var sensor   = root.AddComponent<EnemySensor>();
            var launcher = root.AddComponent<EnemyCylinderLauncher>();

            LinkComponent(health, so =>
            {
                so.FindProperty("_maxHealth").intValue               = 70;
                so.FindProperty("_hitVFX").objectReferenceValue      = hitPs;
                so.FindProperty("_deathShards").objectReferenceValue = shards;
            });
            LinkComponent(shards, so =>
                so.FindProperty("_enemyRenderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());
            LinkComponent(sensor, so =>
            {
                so.FindProperty("_detectionRange").floatValue  = 22f;
                so.FindProperty("_fieldOfView").floatValue     = 180f;
                so.FindProperty("_checkInterval").floatValue   = 0.15f;
                so.FindProperty("_playerLayer").intValue       = playerMask;
                so.FindProperty("_obstructionLayer").intValue  = groundMask;
            });
            LinkComponent(launcher, so =>
            {
                so.FindProperty("_playerLayer").intValue = playerMask;
                so.FindProperty("_groundLayer").intValue = groundMask;
            });

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CyberPulse] Cylinder prefab created at {path}.");
            return prefab;
        }

        private static GameObject CreateCubePrefab(int groundMask, int playerMask, int groundLayerIdx)
        {
            const string path = "Assets/CyberPulse/Prefabs/Cube.prefab";
            EnsureFolder("Assets/CyberPulse/Prefabs");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("Cube_Temp");

            var col = root.AddComponent<BoxCollider>();
            col.size = Vector3.one; col.center = new Vector3(0, 0.5f, 0);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            visual.GetComponent<MeshRenderer>().sharedMaterial = MakeWireframeMaterial();

            var hitFXGO = new GameObject("HitVFX");
            hitFXGO.transform.SetParent(root.transform, false);
            var hitPs = hitFXGO.AddComponent<ParticleSystem>();
            ConfigureHitVFX(hitPs);

            var health   = root.AddComponent<EnemyHealth>();
            var shards   = root.AddComponent<EnemyDeathShards>();
            var sensor   = root.AddComponent<EnemySensor>();
            var splitter = root.AddComponent<EnemyCubeSplitter>();

            LinkComponent(health, so =>
            {
                so.FindProperty("_maxHealth").intValue               = 100;
                so.FindProperty("_hitVFX").objectReferenceValue      = hitPs;
                so.FindProperty("_deathShards").objectReferenceValue = shards;
            });
            LinkComponent(shards, so =>
                so.FindProperty("_enemyRenderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());
            LinkComponent(sensor, so =>
            {
                so.FindProperty("_detectionRange").floatValue  = 25f;
                so.FindProperty("_fieldOfView").floatValue     = 360f;
                so.FindProperty("_checkInterval").floatValue   = 0.2f;
                so.FindProperty("_playerLayer").intValue       = playerMask;
                so.FindProperty("_obstructionLayer").intValue  = groundMask;
            });
            LinkComponent(splitter, so =>
                so.FindProperty("_playerLayer").intValue = playerMask);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CyberPulse] Cube prefab created at {path}.");
            return prefab;
        }

        private static GlitchRendererFeature EnsureGlitchRendererFeature()
        {
            // Find the active UniversalRendererData asset
            var guids = AssetDatabase.FindAssets("t:UniversalRendererData");
            UniversalRendererData rendererData = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var rd   = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (rd != null) { rendererData = rd; break; }
            }

            if (rendererData == null)
            {
                Debug.LogWarning("[CyberPulse] UniversalRendererData not found — GlitchEffect not wired.");
                return null;
            }

            // Reuse existing feature if already present
            foreach (var f in rendererData.rendererFeatures)
            {
                if (f is GlitchRendererFeature existing)
                {
                    EnsureGlitchMaterial(existing, rendererData);
                    return existing;
                }
            }

            // Create and add the feature
            var feature  = ScriptableObject.CreateInstance<GlitchRendererFeature>();
            feature.name = "CyberPulse_GlitchEffect";
            EnsureGlitchMaterial(feature, rendererData);

            AssetDatabase.AddObjectToAsset(feature, AssetDatabase.GetAssetPath(rendererData));
            rendererData.rendererFeatures.Add(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();

            Debug.Log("[CyberPulse] GlitchRendererFeature added to URP renderer.");
            return feature;
        }

        private static void EnsureGlitchMaterial(GlitchRendererFeature feature, UniversalRendererData rendererData)
        {
            if (feature.material != null) return;

            var glitchShader = Shader.Find("CyberPulse/GlitchEffect");
            if (glitchShader == null)
            {
                Debug.LogWarning("[CyberPulse] CyberPulse/GlitchEffect shader not found — glitch material not created.");
                return;
            }

            var mat       = new Material(glitchShader) { name = "M_GlitchEffect" };
            var matPath   = "Assets/CyberPulse/Materials/M_GlitchEffect.mat";
            EnsureFolder("Assets/CyberPulse/Materials");
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            feature.material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
        }

        private static Volume BuildCriticalVolume()
        {
            var go  = new GameObject("Vol_Critical");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.weight   = 0f;
            vol.priority = 15f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // Tighter vignette — increases perceived danger
            var vignette = profile.Add<Vignette>();
            vignette.active = true;
            vignette.intensity.Override(0.55f);
            vignette.smoothness.Override(0.4f);
            vignette.color.Override(new Color(0.8f, 0.1f, 0.05f));

            // Persistent mild chromatic aberration (distinct from the damage spike)
            var ca = profile.Add<ChromaticAberration>();
            ca.active = true;
            ca.intensity.Override(0.35f);

            // Desaturate + red tint — world feels corrupt / hostile
            var colorAdj = profile.Add<ColorAdjustments>();
            colorAdj.active = true;
            colorAdj.colorFilter.Override(new Color(1f, 0.7f, 0.65f));
            colorAdj.saturation.Override(-25f);

            vol.profile = profile;
            return vol;
        }

        private static Volume BuildDamageVolume()
        {
            var go  = new GameObject("Vol_Damage");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.weight   = 0f;
            vol.priority = 20f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var ca = profile.Add<ChromaticAberration>();
            ca.active = true;
            ca.intensity.Override(0.85f);

            // Subtle red-tint + saturation boost for the hit flash feel
            var colorAdj = profile.Add<ColorAdjustments>();
            colorAdj.active = true;
            colorAdj.colorFilter.Override(new Color(1f, 0.6f, 0.6f));
            colorAdj.saturation.Override(-15f);

            vol.profile = profile;
            return vol;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Exit zone — north end of arena, active only during Extract phase
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildExitZone(int playerMask)
        {
            var go = new GameObject("ExitZone");
            go.transform.position = new Vector3(0f, 1.5f, 26f);

            // Visual marker — flat emissive plane so player can see the goal
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "ExitMarker";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale    = new Vector3(6f, 3f, 0.2f);
            visual.transform.localPosition = Vector3.zero;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor",     new Color(0f, 0.8f, 0.2f, 1f));
            mat.SetColor("_EmissionColor", new Color(0f, 2f,   0.4f, 1f));
            mat.EnableKeyword("_EMISSION");
            visual.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // "EXIT" label on the panel
            var labelGO = new GameObject("ExitLabel");
            labelGO.transform.SetParent(go.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, 0.2f, -0.15f);
            var tmp = labelGO.AddComponent<TMPro.TextMeshPro>();
            tmp.text      = "EXIT";
            tmp.fontSize  = 5f;
            tmp.color     = new Color(0f, 1f, 0.4f, 1f);
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(6f, 2f);

            // Point light so it's visible from across the arena
            var lightGO = new GameObject("ExitLight");
            lightGO.transform.SetParent(go.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 0f, -0.5f);
            var lt = lightGO.AddComponent<Light>();
            lt.type      = LightType.Point;
            lt.color     = new Color(0f, 1f, 0.3f);
            lt.intensity = 8f;
            lt.range     = 18f;
            lt.shadows   = LightShadows.None;

            // Trigger collider
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size      = new Vector3(6f, 3f, 2f);

            // ExitTrigger script
            var trigger = go.AddComponent<ExitTrigger>();
            LinkComponent(trigger, so =>
                so.FindProperty("_playerLayer").intValue = playerMask);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Data nodes — 4 placed around the arena, shoot to siphon
        // ──────────────────────────────────────────────────────────────────────

        private static void BuildDataNodes()
        {
            var root = new GameObject("DataNodes");

            Vector3[] positions =
            {
                new Vector3(-12f, 1.2f,  10f),
                new Vector3( 12f, 1.2f,  10f),
                new Vector3(  0f, 1.2f,  18f),
                new Vector3(  0f, 1.2f,  -5f),
            };

            foreach (var pos in positions)
                CreateDataNode(root, pos);
        }

        private static void CreateDataNode(GameObject parent, Vector3 position)
        {
            var go = new GameObject("DataNode");
            go.transform.SetParent(parent.transform, false);
            go.transform.position = position;

            // Visual — sphere
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * 0.5f;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<SphereCollider>());

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor", new Color(0f, 0.96f, 1f));
            mat.SetColor("_EmissionColor", new Color(0f, 2.4f, 2.5f));
            mat.EnableKeyword("_EMISSION");
            visual.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Collider on root (hit target for raycasts)
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.35f;

            // Point light for glow
            var lightGO = new GameObject("NodeLight");
            lightGO.transform.SetParent(go.transform, false);
            var lt = lightGO.AddComponent<Light>();
            lt.type      = LightType.Point;
            lt.color     = new Color(0f, 0.96f, 1f);
            lt.intensity = 3f;
            lt.range     = 4f;
            lt.shadows   = LightShadows.None;

            // Activate VFX — small burst
            var vfxGO = new GameObject("ActivateVFX");
            vfxGO.transform.SetParent(go.transform, false);
            var ps = vfxGO.AddComponent<ParticleSystem>();
            ConfigureNodeVFX(ps);

            // DataNode script
            var node = go.AddComponent<DataNode>();
            LinkComponent(node, so =>
            {
                so.FindProperty("_renderer").objectReferenceValue   = visual.GetComponent<MeshRenderer>();
                so.FindProperty("_nodeLight").objectReferenceValue  = lt;
                so.FindProperty("_activateVFX").objectReferenceValue = ps;
            });
        }

        private static void ConfigureNodeVFX(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration      = 0.3f;
            main.loop          = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize     = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor    = new ParticleSystem.MinMaxGradient(new Color(0f, 1f, 0.4f));
            main.maxParticles  = 40;
            main.playOnAwake   = false;
            main.stopAction    = ParticleSystemStopAction.Disable;

            var emission = ps.emission;
            emission.enabled      = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.2f;

            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                MakeParticleMaterial(new Color(0f, 1f, 0.4f));
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
            var gridShader = Shader.Find("CyberPulse/GridFloor");
            if (gridShader != null)
            {
                var mat = new Material(gridShader) { name = "M_Grid_HLSL" };
                // World-space grid: 1 cell per unit, line width 4% of cell
                mat.SetFloat("_GridScale",         1f);
                mat.SetFloat("_LineWidth",         0.04f);
                mat.SetColor("_BaseColor",         DarkFloor);
                mat.SetColor("_GridColor",         new Color(0f, 0.96f, 1f, 1f));
                mat.SetColor("_GlowColor",         new Color(0.3f, 1.2f, 1.2f, 1f));
                mat.SetFloat("_EmissiveIntensity", 2.5f);
                mat.SetFloat("_GlowRadius",        10f);
                return mat;
            }

            // Fallback to texture-based grid if shader not yet compiled
            Debug.LogWarning("[CyberPulse] CyberPulse/GridFloor shader not found — using fallback material.");
            var fallback = new Material(UrpLit()) { name = "M_Grid" };
            var tex  = MakeGridTex(512, 32, DarkFloor, new Color(0f, 0.96f, 1f, 0.4f));
            var emit = MakeGridTex(512, 32, Color.black, CyanEmit * 0.3f);
            fallback.SetTexture("_BaseMap",  tex);
            fallback.SetColor("_BaseColor",  Color.white);
            fallback.SetTexture("_EmissionMap", emit);
            fallback.SetColor("_EmissionColor", CyanEmit * 0.3f);
            fallback.EnableKeyword("_EMISSION");
            fallback.SetTextureScale("_BaseMap",     new Vector2(60f, 60f));
            fallback.SetTextureScale("_EmissionMap", new Vector2(60f, 60f));
            return fallback;
        }

        private static Material MakeWireframeMaterial()
        {
            var wireShader = Shader.Find("CyberPulse/WireframeEnemy");
            if (wireShader != null)
            {
                var mat = new Material(wireShader) { name = "M_Enemy_Wireframe" };
                mat.SetColor("_EdgeColor",     new Color(2.4f, 0.4f, 0.05f, 1f));  // HDR orange-red
                mat.SetColor("_FillColor",     new Color(0.08f, 0.01f, 0.01f, 1f));
                mat.SetFloat("_FresnelPower",  3.5f);
                mat.SetFloat("_EdgeWidth",     0.55f);
                mat.SetFloat("_PulseSpeed",    1.8f);
                mat.SetFloat("_PulseAmount",   0.25f);
                mat.SetFloat("_EmissiveScale", 3.5f);
                return mat;
            }

            Debug.LogWarning("[CyberPulse] CyberPulse/WireframeEnemy shader not found — using fallback material.");
            return MakeSolidMaterial(EnemyColor);
        }

        private static Material MakeWallEmissiveMaterial()
        {
            var mat = new Material(UrpLit()) { name = "M_Wall_Emissive" };
            mat.SetColor("_BaseColor",     WallColor);
            // Start with a very dim emissive — AudioReactor will boost it at runtime
            mat.SetColor("_EmissionColor", new Color(0.04f, 0.05f, 0.10f));
            mat.EnableKeyword("_EMISSION");
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
