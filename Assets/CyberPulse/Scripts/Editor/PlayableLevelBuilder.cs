using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AI;
using Unity.AI.Navigation;
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

        private const int   LayerCount  = 3;
        private const float LayerHeight = 14f;
        private const float FloorHalf   = 26f;
        private const float ArenaHalf   = 24f;
        private const float HoleXMin = 14f, HoleXMax = FloorHalf;
        private const float HoleZMin = 8f,  HoleZMax = 20f;

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

            var layers = BuildLayeredArena(groundIdx, playerMask);
            BuildLighting();

            var player = BuildPlayer(inputReader, groundMask, playerIdx);
            BuildDebugUI(player);
            BuildSystems(inputReader, player, groundMask, playerMask, groundIdx, layers);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = player;
            SceneView.lastActiveSceneView?.FrameSelected();

            EnsureSceneInBuildSettings(ScenePath, 1);

            Debug.Log("[CyberPulse] Playable level ready — press Play to test.");
        }

        /// <summary>
        /// Ensures <paramref name="scenePath"/> is present and enabled in Build Settings at
        /// the given index (so SceneManager.LoadScene by name resolves it). Idempotent.
        /// </summary>
        private static void EnsureSceneInBuildSettings(string scenePath, int index)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath);
            var entry = new EditorBuildSettingsScene(scenePath, true);
            scenes.Insert(Mathf.Clamp(index, 0, scenes.Count), entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static ArenaLayer[] BuildLayeredArena(int groundLayerIdx, int playerMask)
        {
            var layers = new ArenaLayer[LayerCount];

            for (int i = 0; i < LayerCount; i++)
                layers[i] = BuildLayer(i, groundLayerIdx, playerMask);

            var ascent = new GameObject("Ascent");
            for (int i = 0; i < LayerCount - 1; i++)
            {
                var door = BuildAscent(ascent, i, playerMask);
                LinkComponent(layers[i], so =>
                    so.FindProperty("_exitDoor").objectReferenceValue = door);
            }

            return layers;
        }

        private static ArenaLayer BuildLayer(int index, int groundLayerIdx, int playerMask)
        {
            float floorY = index * LayerHeight;

            var root  = new GameObject($"Layer_{index}");
            var layer = root.AddComponent<ArenaLayer>();

            BuildLayerFloor(root, index, floorY, groundLayerIdx);
            BuildLayerWalls(root, floorY, groundLayerIdx);
            BuildLayerLight(root, floorY);

            var genGO = new GameObject("Generator");
            genGO.transform.SetParent(root.transform, false);
            var generator = genGO.AddComponent<ProceduralArenaGenerator>();
            LinkComponent(generator, so =>
            {
                so.FindProperty("_groundLayerIndex").intValue      = groundLayerIdx;
                so.FindProperty("_playerLayer").intValue           = playerMask;
                so.FindProperty("_floorY").floatValue              = floorY;
                so.FindProperty("_seedOffset").intValue            = index;
            });
            var shake = genGO.AddComponent<WorldBeatShake>();
            LinkComponent(shake, so =>
                so.FindProperty("_elevatedSkipY").floatValue = floorY + 2.5f);

            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry    = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;

            BuildLayerDataNodes(root, floorY);

            LinkComponent(layer, so =>
            {
                so.FindProperty("_index").intValue                  = index;
                so.FindProperty("_floorY").floatValue               = floorY;
                so.FindProperty("_arenaHalf").floatValue            = ArenaHalf;
                so.FindProperty("_generator").objectReferenceValue  = generator;
                so.FindProperty("_navSurface").objectReferenceValue = surface;
                so.FindProperty("_obstacleMask").intValue           = 1 << groundLayerIdx;
            });

            return layer;
        }

        private static void BuildLayerFloor(GameObject root, int index, float floorY, int groundLayerIdx)
        {
            var gridMat = MakeGridMaterial();

            if (index == 0)
            {
                var floor = MakeStaticBox(root, "Floor",
                    new Vector3(0f, floorY - 0.25f, 0f),
                    new Vector3(FloorHalf * 2f, 0.5f, FloorHalf * 2f), DarkFloor, groundLayerIdx);
                floor.GetComponent<MeshRenderer>().sharedMaterial = gridMat;
                return;
            }

            MakeFloorSlab(root, "Floor_W",  -FloorHalf, HoleXMin, -FloorHalf, FloorHalf, floorY, groundLayerIdx, gridMat);
            MakeFloorSlab(root, "Floor_SE",  HoleXMin,  HoleXMax, -FloorHalf, HoleZMin,  floorY, groundLayerIdx, gridMat);
            MakeFloorSlab(root, "Floor_NE",  HoleXMin,  HoleXMax,  HoleZMax,  FloorHalf, floorY, groundLayerIdx, gridMat);
        }

        private static void MakeFloorSlab(GameObject root, string name,
            float xMin, float xMax, float zMin, float zMax, float floorY, int groundLayerIdx, Material mat)
        {
            var slab = MakeStaticBox(root, name,
                new Vector3((xMin + xMax) * 0.5f, floorY - 0.25f, (zMin + zMax) * 0.5f),
                new Vector3(xMax - xMin, 0.5f, zMax - zMin), DarkFloor, groundLayerIdx);
            slab.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void BuildLayerWalls(GameObject root, float floorY, int groundLayerIdx)
        {
            var   wallMat = MakeWallEmissiveMaterial();
            float cy      = floorY + LayerHeight * 0.5f;
            const float t = 0.5f;
            float span    = FloorHalf * 2f + t;

            var n = MakeStaticBox(root, "Wall_North", new Vector3(0f, cy,  FloorHalf), new Vector3(span, LayerHeight, t), WallColor, groundLayerIdx);
            var s = MakeStaticBox(root, "Wall_South", new Vector3(0f, cy, -FloorHalf), new Vector3(span, LayerHeight, t), WallColor, groundLayerIdx);
            var e = MakeStaticBox(root, "Wall_East",  new Vector3( FloorHalf, cy, 0f), new Vector3(t, LayerHeight, span), WallColor, groundLayerIdx);
            var w = MakeStaticBox(root, "Wall_West",  new Vector3(-FloorHalf, cy, 0f), new Vector3(t, LayerHeight, span), WallColor, groundLayerIdx);
            n.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
            s.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
            e.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
            w.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        }

        private static void BuildLayerLight(GameObject root, float floorY)
        {
            var go = new GameObject("LayerLight");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, floorY + 8f, 0f);
            var pt = go.AddComponent<Light>();
            pt.type      = LightType.Point;
            pt.color     = CyanHDR;
            pt.intensity = 3.2f;
            pt.range     = 42f;
            pt.shadows   = LightShadows.None;
        }

        private static void BuildLayerDataNodes(GameObject root, float floorY)
        {
            int count = UnityEngine.Random.Range(3, 5);
            var placed = new Vector3[count];
            int n = 0;

            for (int attempt = 0; attempt < 300 && n < count; attempt++)
            {
                float x = UnityEngine.Random.Range(-ArenaHalf, ArenaHalf);
                float z = UnityEngine.Random.Range(-18f, ArenaHalf);

                if (x > HoleXMin - 1f && z > HoleZMin - 1f && z < HoleZMax + 1f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(0f, -22f)) < 7f) continue;

                var c = new Vector3(x, floorY + 1.2f, z);
                bool ok = true;
                for (int j = 0; j < n; j++)
                    if (Vector3.Distance(c, placed[j]) < 8f) { ok = false; break; }
                if (ok) placed[n++] = c;
            }

            for (int i = 0; i < n; i++)
                CreateDataNode(root, placed[i], startHidden: i > 0);
        }

        private static LockedDoor BuildAscent(GameObject ascentRoot, int lowerIndex, int playerMask)
        {
            float floorYLower = lowerIndex * LayerHeight;
            float floorYUpper = (lowerIndex + 1) * LayerHeight;

            BuildRamp(ascentRoot, lowerIndex, floorYLower, floorYUpper);
            return BuildLockedDoor(ascentRoot, lowerIndex + 1, floorYUpper, playerMask);
        }

        private static void BuildRamp(GameObject ascentRoot, int lowerIndex, float floorYLower, float floorYUpper)
        {
            const float zBot = -16f, zTop = HoleZMax;
            float rise     = floorYUpper - floorYLower;
            float run      = zTop - zBot;
            float angle    = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float slopeLen = Mathf.Sqrt(run * run + rise * rise);

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name  = $"Ramp_{lowerIndex}_to_{lowerIndex + 1}";
            ramp.layer = LayerMask.NameToLayer(GroundLayerName);
            ramp.transform.SetParent(ascentRoot.transform, false);
            ramp.transform.position   = new Vector3((HoleXMin + HoleXMax) * 0.5f,
                                                    floorYLower + rise * 0.5f,
                                                    (zBot + zTop) * 0.5f);
            ramp.transform.rotation   = Quaternion.Euler(-angle, 0f, 0f);
            ramp.transform.localScale = new Vector3(10f, 0.4f, slopeLen);

            var mat = new Material(UrpLit());
            mat.SetColor("_BaseColor",     PlatColor);
            mat.SetColor("_EmissionColor", new Color(0f, 0.4f, 0.6f));
            mat.EnableKeyword("_EMISSION");
            ramp.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static LockedDoor BuildLockedDoor(GameObject ascentRoot, int upperIndex, float floorYUpper, int playerMask)
        {
            float cx = (HoleXMin + HoleXMax) * 0.5f;
            float cz = (HoleZMin + HoleZMax) * 0.5f;
            float w  = HoleXMax - HoleXMin;
            float d  = HoleZMax - HoleZMin;
            const float plugDepth = 5f;

            var go = new GameObject($"Door_to_Layer{upperIndex}");
            go.transform.SetParent(ascentRoot.transform, false);
            go.transform.position = new Vector3(cx, floorYUpper, cz);

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center    = new Vector3(0f, 1.3f, (d * 0.5f) + 3f);
            trigger.size      = new Vector3(w, 3f, 6f);

            var door = go.AddComponent<LockedDoor>();

            var hatch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hatch.name = "Hatch";
            UnityEngine.Object.DestroyImmediate(hatch.GetComponent<BoxCollider>());
            hatch.transform.SetParent(go.transform, false);
            hatch.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            hatch.transform.localScale    = new Vector3(w, 0.3f, d);
            var hatchMat = new Material(UrpLit());
            hatchMat.SetColor("_BaseColor", WallColor);
            hatchMat.EnableKeyword("_EMISSION");
            hatch.GetComponent<MeshRenderer>().sharedMaterial = hatchMat;

            var plugGO = new GameObject("Plug");
            plugGO.layer = LayerMask.NameToLayer(GroundLayerName);
            plugGO.transform.SetParent(go.transform, false);
            plugGO.transform.localPosition = new Vector3(0f, -plugDepth * 0.5f, 0f);
            var plug = plugGO.AddComponent<BoxCollider>();
            plug.size = new Vector3(w, plugDepth, d);

            LinkComponent(door, so =>
            {
                so.FindProperty("_panel").objectReferenceValue   = hatch.GetComponent<MeshRenderer>();
                so.FindProperty("_blocker").objectReferenceValue = plug;
                so.FindProperty("_playerLayer").intValue         = playerMask;
            });

            return door;
        }

        private static void BuildLighting()
        {
            var dirGO = new GameObject("Directional Light");
            dirGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var dir = dirGO.AddComponent<Light>();
            dir.type      = LightType.Directional;
            dir.intensity = 0.25f;
            dir.color     = new Color(0.85f, 0.85f, 1f);
            dir.shadows   = LightShadows.Soft;

        }

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

            LinkComponent(playerStats, so =>
                so.FindProperty("_maxHealth").intValue = 9999);

            var pivotGO = new GameObject("CameraPivot");
            pivotGO.transform.SetParent(player.transform, false);
            pivotGO.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var camScript = pivotGO.AddComponent<PlayerCamera>();

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

            var fxRoot = new GameObject("FX");
            fxRoot.transform.SetParent(player.transform, false);
            var psGO = new GameObject("DashParticles");
            psGO.transform.SetParent(fxRoot.transform, false);
            var dashPs = psGO.AddComponent<ParticleSystem>();
            ConfigureDashParticles(dashPs);

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

            var sfxArFire   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/ar_fire.mp3");
            var sfxArReload = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/ar_reload.mp3");
            if (sfxArFire   == null) Debug.LogWarning("[CyberPulse] Assets/Audio/Sfx/ar_fire.mp3 not found.");
            if (sfxArReload == null) Debug.LogWarning("[CyberPulse] Assets/Audio/Sfx/ar_reload.mp3 not found.");

            LinkComponent(hitscan, so =>
            {
                so.FindProperty("_weaponName").stringValue            = "Assault Rifle";
                so.FindProperty("_specialCost").floatValue            = 60f;
                so.FindProperty("_muzzleFlash").objectReferenceValue  = muzzlePs;
                so.FindProperty("_audioSource").objectReferenceValue  = weaponAudio;
                if (sfxArFire   != null)
                    so.FindProperty("_fireClip").objectReferenceValue   = sfxArFire;
                if (sfxArReload != null)
                    so.FindProperty("_reloadClip").objectReferenceValue = sfxArReload;
            });
            LinkComponent(sway, so =>
            {
                so.FindProperty("_input").objectReferenceValue      = inputReader;
                so.FindProperty("_controller").objectReferenceValue = controller;
            });

            var sfxRevolverShot   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/revolver_shot.mp3");
            var sfxRevolverReload = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/revolver_reload.mp3");
            var sfxShotgunShot    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/shotgun_shot.mp3");
            var sfxShotgunReload  = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/shotgun_reload.mp3");

            if (sfxRevolverShot   == null) Debug.LogWarning("[CyberPulse] Assets/Audio/Sfx/revolver_shot.mp3 not found.");
            if (sfxRevolverReload == null) Debug.LogWarning("[CyberPulse] Assets/Audio/Sfx/revolver_reload.mp3 not found.");
            if (sfxShotgunShot    == null) Debug.LogWarning("[CyberPulse] Assets/Audio/Sfx/shotgun_shot.mp3 not found.");
            if (sfxShotgunReload  == null) Debug.LogWarning("[CyberPulse] Assets/Audio/Sfx/shotgun_reload.mp3 not found.");

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
                so.FindProperty("_spreadAngle").floatValue            = 24f;
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

            player.AddComponent<GridFloorUpdater>();

            return player;
        }

        private static void BuildDebugUI(GameObject player)
        {
            var go        = new GameObject("DebugUI");
            var crosshair = go.AddComponent<CrosshairUI>();
            var hud       = go.AddComponent<DiegeticHUD>();

            LinkComponent(crosshair, so =>
            {
                so.FindProperty("_controller").objectReferenceValue   = player.GetComponent<PlayerController>();
                so.FindProperty("_weaponHolder").objectReferenceValue = player.GetComponent<WeaponHolder>();
            });
            LinkComponent(hud, so =>
            {
                so.FindProperty("_playerStats").objectReferenceValue  = player.GetComponent<PlayerStats>();
                so.FindProperty("_weaponHolder").objectReferenceValue = player.GetComponent<WeaponHolder>();
            });
        }

        private static void BuildEnemies(int groundMask, int playerMask, int groundLayerIdx)
        {
            var root = new GameObject("Enemies");

            CreateEnemy("Enemy_NW", root, new Vector3(-20f, 0f, 20f), groundMask, playerMask,
                new[]
                {
                    new Vector3(-22f, 0f, 22f), new Vector3(-10f, 0f, 22f),
                    new Vector3(-10f, 0f, 10f), new Vector3(-22f, 0f, 10f),
                });

            CreateEnemy("Enemy_NE", root, new Vector3(8f, 0f, 20f), groundMask, playerMask,
                new[]
                {
                    new Vector3(10f, 0f, 24f), new Vector3( 5f, 0f, 24f),
                    new Vector3( 5f, 0f, 12f), new Vector3(10f, 0f, 12f),
                });

            CreateEnemy("Enemy_W", root, new Vector3(-20f, 0f, -5f), groundMask, playerMask,
                new[]
                {
                    new Vector3(-22f, 0f, -3f), new Vector3(-12f, 0f, -3f),
                    new Vector3(-12f, 0f, -15f), new Vector3(-22f, 0f, -15f),
                });

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

            var col = enemy.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.4f;
            col.center = new Vector3(0f, 1f, 0f);

            var agent = enemy.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.height          = 2f;
            agent.radius          = 0.4f;
            agent.angularSpeed    = 360f;
            agent.acceleration    = 12f;
            agent.stoppingDistance = 0.5f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(enemy.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
            visual.GetComponent<MeshRenderer>().sharedMaterial = MakeWireframeMaterial();

            var hitFXGO = new GameObject("HitVFX");
            hitFXGO.transform.SetParent(enemy.transform, false);
            var hitPs = hitFXGO.AddComponent<ParticleSystem>();
            ConfigureHitVFX(hitPs);

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

            var health     = enemy.AddComponent<EnemyHealth>();
            var shards     = enemy.AddComponent<EnemyDeathShards>();
            var sensor     = enemy.AddComponent<EnemySensor>();
            var attack     = enemy.AddComponent<EnemyAttack>();
            var controller = enemy.AddComponent<EnemyController>();

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

        private static void BuildSystems(InputReader inputReader, GameObject player,
            int groundMask, int playerMask, int groundLayerIdx, ArenaLayer[] layers)
        {
            var go = new GameObject("GameSystems");
            var gameManager = go.AddComponent<GameManager>();
            go.AddComponent<DataNodeManager>();

            var layerManager = go.AddComponent<LayerManager>();
            LinkComponent(layerManager, so =>
            {
                var arr = so.FindProperty("_layers");
                arr.arraySize = layers.Length;
                for (int i = 0; i < layers.Length; i++)
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
            });

            var winOverlay  = BuildEndScreen("WinScreen",  "EXTRACTION COMPLETE",
                new Color(0f,   1f,    0.35f, 1f), new Color(0f,    0.04f, 0f,    0.80f));
            var failOverlay = BuildEndScreen("FailScreen", "TRACED",
                new Color(1f,   0.12f, 0.05f, 1f), new Color(0.06f, 0f,    0f,    0.80f));
            LinkComponent(gameManager, so =>
            {
                so.FindProperty("_winOverlay").objectReferenceValue  = winOverlay;
                so.FindProperty("_failOverlay").objectReferenceValue = failOverlay;
            });
            var traceMeter = go.AddComponent<TraceMeter>();
            go.AddComponent<PhaseVisuals>();
            go.AddComponent<ScoreManager>();
            go.AddComponent<AudioAnalyzer>();
            var songAnalyzer = go.AddComponent<SongAnalyzer>();
            var beatClock    = go.AddComponent<BeatClock>();
            go.AddComponent<SyncGauge>();

            AudioSource ambSrc = null;
            var musicGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/Music" });
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
            if (ambientClip == null && actionClip != null) { ambientClip = actionClip; }
            if (actionClip  == null && ambientClip != null)  actionClip  = ambientClip;

            if (ambientClip != null)
            {
                ambSrc             = go.AddComponent<AudioSource>();
                ambSrc.clip        = ambientClip;
                ambSrc.playOnAwake = false;
                ambSrc.loop        = false;
                ambSrc.spatialBlend = 0f;
                ambSrc.volume      = 1f;

                var actSrc         = go.AddComponent<AudioSource>();
                actSrc.clip        = actionClip;
                actSrc.playOnAwake = false;
                actSrc.loop        = false;
                actSrc.spatialBlend = 0f;
                actSrc.volume      = 0f;

                var dynMusic = go.AddComponent<DynamicMusicPlayer>();
                LinkComponent(dynMusic, so =>
                {
                    so.FindProperty("_ambientSrc").objectReferenceValue = ambSrc;
                    so.FindProperty("_actionSrc").objectReferenceValue  = actSrc;
                    so.FindProperty("_playOnStart").boolValue           = false;
                });
                Debug.Log($"[CyberPulse] Dynamic music wired: ambient={ambientClip.name}, action={actionClip.name}");

                LinkComponent(beatClock, so =>
                    so.FindProperty("_musicSource").objectReferenceValue = ambSrc);
                LinkComponent(songAnalyzer, so =>
                {
                    so.FindProperty("_musicSource").objectReferenceValue = ambSrc;
                    so.FindProperty("_analyzeOnStart").boolValue         = false;
                });

                var songs   = GatherSongs();
                var loading = go.AddComponent<LoadingController>();
                LinkComponent(loading, so =>
                {
                    so.FindProperty("_ambientSrc").objectReferenceValue  = ambSrc;
                    so.FindProperty("_actionSrc").objectReferenceValue   = actSrc;
                    so.FindProperty("_analyzer").objectReferenceValue    = songAnalyzer;
                    so.FindProperty("_musicPlayer").objectReferenceValue = dynMusic;
                    var songsProp = so.FindProperty("_songs");
                    songsProp.arraySize = songs.Length;
                    for (int i = 0; i < songs.Length; i++)
                        songsProp.GetArrayElementAtIndex(i).objectReferenceValue = songs[i];
                });
            }

            var envReactor = go.AddComponent<EnvironmentAudioReactor>();
            var analyzer   = go.GetComponent<AudioAnalyzer>();
            LinkComponent(envReactor, so =>
                so.FindProperty("_analyzer").objectReferenceValue = analyzer);

            go.AddComponent<TimeManager>();
            go.AddComponent<CyberPulse.UI.PauseMenu>();

            var damageVol   = BuildDamageVolume();
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

            LinkComponent(traceMeter, so =>
            {
                so.FindProperty("_criticalVolume").objectReferenceValue = critVol;
                so.FindProperty("_playerStats").objectReferenceValue    = playerStats;
                if (ambSrc != null)
                    so.FindProperty("_musicSource").objectReferenceValue = ambSrc;
            });

            var dataBits = go.AddComponent<DataBitRenderer>();
            LinkComponent(dataBits, so =>
                so.FindProperty("_player").objectReferenceValue = player.transform);

            var glitchFeature = EnsureGlitchRendererFeature();
            var glitchCtrl    = go.AddComponent<GlitchController>();
            LinkComponent(glitchCtrl, so =>
            {
                so.FindProperty("_playerStats").objectReferenceValue = playerStats;
                if (glitchFeature != null)
                    so.FindProperty("_feature").objectReferenceValue = glitchFeature;
            });

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
                so.FindProperty("_obstacleMask").intValue = groundMask;
                if (seekerPrefab   != null) so.FindProperty("_seekerPrefab").objectReferenceValue   = seekerPrefab;
                if (spherePrefab   != null) so.FindProperty("_spherePrefab").objectReferenceValue   = spherePrefab;
                if (trianglePrefab != null) so.FindProperty("_trianglePrefab").objectReferenceValue = trianglePrefab;
                if (cylinderPrefab != null) so.FindProperty("_cylinderPrefab").objectReferenceValue = cylinderPrefab;
                if (cubePrefab     != null) so.FindProperty("_cubePrefab").objectReferenceValue     = cubePrefab;
            });

            var dash        = player.GetComponent<DashAbility>();
            var controller  = player.GetComponent<PlayerController>();
            var beatReactor = go.AddComponent<BeatReactor>();
            LinkComponent(beatReactor, so =>
            {
                so.FindProperty("_controller").objectReferenceValue   = controller;
                so.FindProperty("_dash").objectReferenceValue         = dash;
                so.FindProperty("_playerStats").objectReferenceValue  = playerStats;
                so.FindProperty("_weaponHolder").objectReferenceValue = player.GetComponent<WeaponHolder>();
            });
        }

        /// <summary>
        /// Creates (or reuses) a Seeker enemy prefab at Assets/CyberPulse/Prefabs/Seeker.prefab.
        /// The prefab has no patrol points so spawned seekers idle in place until they
        /// detect the player via EnemySensor, then chase and attack.
        /// </summary>
        private static GameObject CreateSeekerPrefab(int groundMask, int playerMask, int groundLayerIdx)
        {
            const string prefabPath = "Assets/CyberPulse/Prefabs/Seeker.prefab";
            EnsureFolder("Assets/CyberPulse/Prefabs");

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

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
            var controller = root.AddComponent<EnemyController>();
            var stateVis   = root.AddComponent<EnemyStateVisual>();

            LinkComponent(stateVis, so =>
                so.FindProperty("_renderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());
            LinkComponent(health, so =>
            {
                so.FindProperty("_hitVFX").objectReferenceValue      = hitPs;
                so.FindProperty("_deathShards").objectReferenceValue = shards;
            });
            LinkComponent(shards, so =>
                so.FindProperty("_enemyRenderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());
            LinkComponent(sensor, so =>
            {
                so.FindProperty("_detectionRange").floatValue  = 26f;
                so.FindProperty("_fieldOfView").floatValue     = 220f;
                so.FindProperty("_checkInterval").floatValue   = 0.08f;
                so.FindProperty("_playerLayer").intValue       = playerMask;
                so.FindProperty("_obstructionLayer").intValue  = groundMask;
            });
            LinkComponent(attack, so =>
            {
                so.FindProperty("_damage").intValue      = 15;
                so.FindProperty("_cooldown").floatValue  = 0.9f;
                so.FindProperty("_range").floatValue     = 2.8f;
                so.FindProperty("_playerLayer").intValue = playerMask;
            });
            LinkComponent(controller, so =>
            {
                so.FindProperty("_chaseSpeed").floatValue    = 7f;
                so.FindProperty("_attackRange").floatValue   = 2.8f;
                so.FindProperty("_patrolWaitTime").floatValue = 0.5f;
            });

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CyberPulse] Seeker prefab created at {prefabPath}.");
            return prefab;
        }

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
            var stateV  = root.AddComponent<EnemyStateVisual>();
            LinkComponent(stateV, so =>
                so.FindProperty("_renderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());

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
            var stateV   = root.AddComponent<EnemyStateVisual>();
            LinkComponent(stateV, so =>
                so.FindProperty("_renderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());

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
            var stateV   = root.AddComponent<EnemyStateVisual>();
            LinkComponent(stateV, so =>
                so.FindProperty("_renderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());

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
            var stateV   = root.AddComponent<EnemyStateVisual>();
            LinkComponent(stateV, so =>
                so.FindProperty("_renderer").objectReferenceValue = visual.GetComponent<MeshRenderer>());

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

            foreach (var f in rendererData.rendererFeatures)
            {
                if (f is GlitchRendererFeature existing)
                {
                    EnsureGlitchMaterial(existing, rendererData);
                    return existing;
                }
            }

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

        private static GameObject BuildEndScreen(string name, string headline, Color color, Color bg)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var overlay = go.AddComponent<GameOverlay>();
            LinkComponent(overlay, so =>
            {
                so.FindProperty("_headline").stringValue     = headline;
                so.FindProperty("_headlineColor").colorValue = color;
                so.FindProperty("_bgColor").colorValue       = bg;
            });
            return go;
        }

        private static Volume BuildCriticalVolume()
        {
            var go  = new GameObject("Vol_Critical");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.weight   = 0f;
            vol.priority = 15f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var vignette = profile.Add<Vignette>();
            vignette.active = true;
            vignette.intensity.Override(0.55f);
            vignette.smoothness.Override(0.4f);
            vignette.color.Override(new Color(0.8f, 0.1f, 0.05f));

            var ca = profile.Add<ChromaticAberration>();
            ca.active = true;
            ca.intensity.Override(0.35f);

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

            var colorAdj = profile.Add<ColorAdjustments>();
            colorAdj.active = true;
            colorAdj.colorFilter.Override(new Color(1f, 0.6f, 0.6f));
            colorAdj.saturation.Override(-15f);

            vol.profile = profile;
            return vol;
        }

        private static void CreateDataNode(GameObject parent, Vector3 position, bool startHidden)
        {
            var go = new GameObject("DataNode");
            go.transform.SetParent(parent.transform, false);
            go.transform.position = position;

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

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.35f;

            var lightGO = new GameObject("NodeLight");
            lightGO.transform.SetParent(go.transform, false);
            var lt = lightGO.AddComponent<Light>();
            lt.type      = LightType.Point;
            lt.color     = new Color(0f, 0.96f, 1f);
            lt.intensity = 3f;
            lt.range     = 4f;
            lt.shadows   = LightShadows.None;

            var vfxGO = new GameObject("ActivateVFX");
            vfxGO.transform.SetParent(go.transform, false);
            var ps = vfxGO.AddComponent<ParticleSystem>();
            ConfigureNodeVFX(ps);

            var node = go.AddComponent<DataNode>();
            LinkComponent(node, so =>
            {
                so.FindProperty("_renderer").objectReferenceValue    = visual.GetComponent<MeshRenderer>();
                so.FindProperty("_nodeLight").objectReferenceValue   = lt;
                so.FindProperty("_activateVFX").objectReferenceValue = ps;
                so.FindProperty("_startHidden").boolValue            = startHidden;
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

        private static void BakeNavMesh()
        {
            try
            {
#pragma warning disable CS0618
                NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618
                Debug.Log("[CyberPulse] NavMesh baked successfully.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CyberPulse] NavMesh auto-bake failed: " + e.Message +
                    " — open Window > AI > Navigation and click Bake manually.");
            }
        }

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

        private static Material MakeGridMaterial()
        {
            var gridShader = Shader.Find("CyberPulse/GridFloor");
            if (gridShader != null)
            {
                var mat = new Material(gridShader) { name = "M_Grid_HLSL" };
                mat.SetFloat("_GridScale",         1f);
                mat.SetFloat("_LineWidth",         0.04f);
                mat.SetColor("_BaseColor",         DarkFloor);
                mat.SetColor("_GridColor",         new Color(0f, 0.96f, 1f, 1f));
                mat.SetColor("_GlowColor",         new Color(0.3f, 1.2f, 1.2f, 1f));
                mat.SetFloat("_EmissiveIntensity", 2.5f);
                mat.SetFloat("_GlowRadius",        10f);
                return mat;
            }

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
                mat.SetColor("_EdgeColor",     new Color(2.4f, 0.4f, 0.05f, 1f));
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
#pragma warning disable CS0618
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.NavigationStatic |
                StaticEditorFlags.ContributeGI     |
                StaticEditorFlags.BatchingStatic);
#pragma warning restore CS0618
            return go;
        }

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

        private static void LinkComponent<T>(T component, Action<SerializedObject> assign)
            where T : UnityEngine.Object
        {
            var so = new SerializedObject(component);
            assign(so);
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// All selectable tracks under Assets/Audio/Music (action stems excluded).
        /// LoadingController matches the menu's SongSelection against this set by name.
        /// </summary>
        private static AudioClip[] GatherSongs()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/Music" });
            var list  = new System.Collections.Generic.List<AudioClip>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                string fn = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (fn.Contains("action") || fn.Contains("combat") || fn.Contains("intense")) continue;
                list.Add(clip);
            }
            return list.ToArray();
        }

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
