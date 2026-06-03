using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using TMPro;

namespace CyberPulse.Editor
{
    /// <summary>
    /// Procedurally builds the MovementTestScene from the menu bar.
    /// Every movement system (ground, jump, coyote, wall-slide, dash) gets its own
    /// dedicated area so you can test each feature in isolation on first boot.
    /// </summary>
    public static class MovementTestSceneBuilder
    {

        private const string ScenePath   = "Assets/Scenes/MovementTestScene.unity";
        private const string GroundLayer = "Ground";

        private static readonly Color CyanHDR     = new Color(0f, 0.961f, 1f, 1f);
        private static readonly Color CyanEmit    = new Color(0f, 2.4f,  2.5f, 1f);
        private static readonly Color DarkBg      = new Color(0.04f, 0.04f, 0.08f, 1f);
        private static readonly Color WallColor   = new Color(0.08f, 0.08f, 0.14f, 1f);
        private static readonly Color PlatColor   = new Color(0.05f, 0.12f, 0.18f, 1f);
        private static readonly Color MarkerColor = new Color(0.12f, 0.05f, 0.18f, 1f);
        private static readonly Color PillarColor = new Color(0.06f, 0.06f, 0.12f, 1f);

        [MenuItem("CyberPulse/Build Movement Test Scene")]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureScenesFolder();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.06f);
            RenderSettings.fog          = false;

            int groundLayerIndex = LayerMask.NameToLayer(GroundLayer);
            if (groundLayerIndex < 0)
            {
                Debug.LogWarning(
                    "[MovementTestSceneBuilder] Layer \"Ground\" not found. " +
                    "Create it in Edit > Project Settings > Tags and Layers, then rebuild.");
                groundLayerIndex = 0;
            }

            BuildGround(groundLayerIndex);
            BuildWallSlideCorridors(groundLayerIndex);
            BuildJumpPlatforms(groundLayerIndex);
            BuildDashMarkers(groundLayerIndex);
            BuildAirZonePillars(groundLayerIndex);
            BuildLighting();
            PlaceSpawnMarker();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[MovementTestSceneBuilder] Scene saved to {ScenePath}. " +
                      "Drag the Player prefab to (0, 1, 0) and press Play.");
        }

        private static void BuildGround(int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground";
            go.layer = layer;
            go.transform.localScale = new Vector3(4f, 1f, 4f);

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = CreateGridMaterial();
        }

        private static void BuildWallSlideCorridors(int layer)
        {
            var root = new GameObject("WallSlide_Corridor");
            root.transform.position = new Vector3(10f, 0f, 0f);

            CreateBox(root, "WallA",
                new Vector3( 1.75f, 2.5f, 0f),
                new Vector3(0.25f, 5f, 15f),
                WallColor, layer);

            CreateBox(root, "WallB",
                new Vector3(-1.75f, 2.5f, 0f),
                new Vector3(0.25f, 5f, 15f),
                WallColor, layer);

            CreateBox(root, "CorridorFloor",
                new Vector3(0f, -0.05f, 0f),
                new Vector3(3f, 0.1f, 15f),
                DarkBg, layer);
        }

        private static void BuildJumpPlatforms(int layer)
        {
            var root = new GameObject("JumpPlatforms");
            root.transform.position = new Vector3(-12f, 0f, 5f);

            float[] heights  = { 1f, 2f, 3.5f, 5f, 7f };
            float   spacing  = 4f;

            for (int i = 0; i < heights.Length; i++)
            {
                float h = heights[i];
                CreateBox(root, $"Platform_{i + 1}",
                    new Vector3(i * spacing, h + 0.1f, 0f),
                    new Vector3(3f, 0.2f, 3f),
                    PlatColor, layer);
            }
        }

        private static void BuildDashMarkers(int layer)
        {
            var root = new GameObject("DashMarkers");
            root.transform.position = Vector3.zero;

            int[] distances = { 3, 5, 8, 12, 16, 20 };

            foreach (int d in distances)
            {
                var slab = CreateBox(root, $"Marker_{d}m",
                    new Vector3(0f, 1.5f, d),
                    new Vector3(2f, 3f, 0.1f),
                    MarkerColor, layer);

                var labelGO = new GameObject($"Label_{d}m");
                labelGO.transform.SetParent(slab.transform, false);
                labelGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                labelGO.transform.localRotation = Quaternion.identity;

                var tmp = labelGO.AddComponent<TextMeshPro>();
                tmp.text = $"{d}m";
                tmp.fontSize = 3f;
                tmp.color = CyanHDR;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                tmp.rectTransform.sizeDelta = new Vector2(2f, 1f);
            }
        }

        private static void BuildAirZonePillars(int layer)
        {
            var root = new GameObject("AirZone_Pillars");

            float r = 20f;
            Vector3[] corners =
            {
                new Vector3(-r, 0f, -r),
                new Vector3( r, 0f, -r),
                new Vector3(-r, 0f,  r),
                new Vector3( r, 0f,  r),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                CreateBox(root, $"Pillar_{i + 1}",
                    corners[i] + new Vector3(0f, 3f, 0f),
                    new Vector3(0.6f, 6f, 0.6f),
                    PillarColor, layer);
            }
        }

        private static void BuildLighting()
        {
            var dirGO = new GameObject("Directional Light");
            dirGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var dirLight = dirGO.AddComponent<Light>();
            dirLight.type      = LightType.Directional;
            dirLight.intensity = 0.3f;
            dirLight.color     = new Color(0.8f, 0.8f, 1f);
            dirLight.shadows   = LightShadows.Soft;

            float r = 18f;
            Vector3[] positions =
            {
                new Vector3(-r, 4f, -r),
                new Vector3( r, 4f, -r),
                new Vector3(-r, 4f,  r),
                new Vector3( r, 4f,  r),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var ptGO = new GameObject($"PointLight_Cyan_{i + 1}");
                ptGO.transform.position = positions[i];
                var pt = ptGO.AddComponent<Light>();
                pt.type      = LightType.Point;
                pt.color     = CyanHDR;
                pt.intensity = 3f;
                pt.range     = 12f;
                pt.shadows   = LightShadows.None;
            }
        }

        private static void PlaceSpawnMarker()
        {
            var go = new GameObject("SpawnPoint");
            go.transform.position = new Vector3(0f, 0f, 0f);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "SpawnMarker_Visual";
            sphere.transform.SetParent(go.transform, false);
            sphere.transform.localScale = Vector3.one * 0.25f;
            sphere.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());

            var mr = sphere.GetComponent<MeshRenderer>();
            mr.sharedMaterial = CreateEmissiveMaterial(CyanEmit);

            Debug.Log("[MovementTestSceneBuilder] Spawn marker at (0, 0, 0). " +
                      "Place your Player prefab at (0, 1, 0).");
        }

        /// <summary>Creates a cube primitive with a given size, colour, and layer under a parent.</summary>
        private static GameObject CreateBox(GameObject parent, string name,
            Vector3 localPos, Vector3 size, Color color, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = CreateSolidUrpMaterial(color);
            return go;
        }

        private static Material CreateGridMaterial()
        {
            var mat = new Material(GetUrpLitShader());
            mat.name = "M_GridFloor";

            Texture2D grid = CreateGridTexture(512, 32, DarkBg, new Color(0f, 0.96f, 1f, 0.35f));

            mat.SetTexture("_BaseMap", grid);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_EmissionMap", CreateGridTexture(512, 32, Color.black, CyanEmit * 0.4f));
            mat.SetColor("_EmissionColor", CyanEmit * 0.4f);
            mat.EnableKeyword("_EMISSION");

            mat.SetTextureScale("_BaseMap", new Vector2(40f, 40f));
            mat.SetTextureScale("_EmissionMap", new Vector2(40f, 40f));

            return mat;
        }

        private static Material CreateSolidUrpMaterial(Color color)
        {
            var mat = new Material(GetUrpLitShader());
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        private static Material CreateEmissiveMaterial(Color emitColor)
        {
            var mat = new Material(GetUrpLitShader());
            mat.SetColor("_BaseColor", Color.black);
            mat.SetColor("_EmissionColor", emitColor);
            mat.EnableKeyword("_EMISSION");
            return mat;
        }

        private static Shader GetUrpLitShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            return shader;
        }

        private static Texture2D CreateGridTexture(int size, int cellSize, Color bg, Color line)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onLine = (x % cellSize == 0) || (y % cellSize == 0);
                    pixels[y * size + x] = onLine ? line : bg;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static void EnsureScenesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }
}
