using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using TMPro;
using CyberPulse.Systems;
using CyberPulse.UI;

namespace CyberPulse.Editor
{
    /// <summary>
    /// Builds the main menu scene in one click.
    /// Menu: CyberPulse ▶ Build Main Menu
    ///
    /// Creates Assets/Scenes/MainMenu.unity with:
    ///   - Full-screen cyberpunk UI (title, subtitle, mission brief, two buttons)
    ///   - Atmospheric background camera + cyan point lights + data-bits
    ///   - Fade-in on load, fade-out on transition
    ///   - Title glitch flicker animation
    ///   - Same music track at lower volume
    ///
    /// AFTER BUILDING: add both scenes to File → Build Settings (in order:
    ///   0 = MainMenu, 1 = PlayableTestLevel).
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        private static readonly Color CyanHDR  = new Color(0f,    0.96f, 1f,    1f);
        private static readonly Color DimBlue  = new Color(0.02f, 0.03f, 0.06f, 1f);
        private static readonly Color PanelBG  = new Color(0.04f, 0.05f, 0.09f, 0.88f);
        private static readonly Color BtnNorm  = new Color(0.05f, 0.07f, 0.14f, 1f);
        private static readonly Color BtnBorder= new Color(0f,    0.96f, 1f,    0.7f);
        private static readonly Color TextDim  = new Color(0.45f, 0.55f, 0.65f, 1f);

        // ── Entry point ───────────────────────────────────────────────────────

        [MenuItem("CyberPulse/► Build Main Menu")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.04f);
            RenderSettings.fog          = false;

            var cam    = BuildCamera();
            var ctrl   = BuildSystems(cam);
            BuildCanvas(ctrl);
            AddEventSystem();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[CyberPulse] Main Menu built. " +
                      "Add both scenes to File → Build Settings: 0=MainMenu, 1=PlayableTestLevel.");
        }

        // ── Camera ────────────────────────────────────────────────────────────

        private static GameObject BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            var cam = go.AddComponent<Camera>();
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = DimBlue;
            cam.nearClipPlane    = 0.1f;
            cam.farClipPlane     = 300f;
            cam.fieldOfView      = 60f;

            go.AddComponent<AudioListener>();
            TryEnablePostProcessing(go);

            // Subtle cyan accent lights
            float[] zPos = { -8f, 8f };
            for (int i = 0; i < zPos.Length; i++)
            {
                var lt = new GameObject($"AccentLight_{i}");
                lt.transform.position = new Vector3(i == 0 ? -6f : 6f, 3f, zPos[i]);
                var l = lt.AddComponent<Light>();
                l.type      = LightType.Point;
                l.color     = CyanHDR;
                l.intensity = 3f;
                l.range     = 14f;
                l.shadows   = LightShadows.None;
            }

            return go;
        }

        // ── Systems ───────────────────────────────────────────────────────────

        private static MainMenuController BuildSystems(GameObject cam)
        {
            var go   = new GameObject("MenuSystems");
            var ctrl = go.AddComponent<MainMenuController>();

            // Atmospheric data-bits (fewer, centred on camera)
            var bits = go.AddComponent<DataBitRenderer>();
            var so = new SerializedObject(bits);
            so.FindProperty("_player").objectReferenceValue = cam.transform;
            so.FindProperty("_count").intValue              = 400;
            so.FindProperty("_repelRadius").floatValue      = 0f;   // no repulsion in menu
            so.ApplyModifiedProperties();

            // Music (quieter than gameplay)
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Music" });
            if (guids.Length > 0)
            {
                var clipPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var clip     = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null)
                {
                    var src         = go.AddComponent<AudioSource>();
                    src.clip        = clip;
                    src.playOnAwake = true;
                    src.loop        = true;
                    src.volume      = 0.55f;
                    src.spatialBlend = 0f;
                }
            }

            return ctrl;
        }

        // ── Canvas ────────────────────────────────────────────────────────────

        private static void BuildCanvas(MainMenuController ctrl)
        {
            // Root canvas — Screen Space Overlay, scales with 1080p reference
            var cGO = new GameObject("MenuCanvas");
            var canvas = cGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = cGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            cGO.AddComponent<GraphicRaycaster>();

            var root = cGO.GetComponent<RectTransform>();

            // ── Fade panel (fullscreen black — starts opaque, fades away) ─────
            var fadeGO = new GameObject("FadePanel");
            fadeGO.transform.SetParent(cGO.transform, false);
            var fade   = fadeGO.AddComponent<Image>();
            fade.color = new Color(0f, 0f, 0f, 1f);
            var frt = fade.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            fade.raycastTarget = false;

            // ── Background subtle vignette panel ─────────────────────────────
            var vignGO = new GameObject("Vignette");
            vignGO.transform.SetParent(cGO.transform, false);
            var vign = vignGO.AddComponent<Image>();
            vign.color = new Color(0f, 0f, 0f, 0.35f);
            var vrt = vign.rectTransform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = vrt.offsetMax = Vector2.zero;
            vign.raycastTarget = false;

            // ── CYBER-PULSE title ─────────────────────────────────────────────
            var titleTMP = MakeTMP(root, "Title", "CYBER-PULSE", 128,
                CyanHDR, TextAlignmentOptions.Center,
                new Vector2(0f, 0.55f), new Vector2(1f, 0.88f));
            titleTMP.fontStyle = FontStyles.Bold;

            // ── Subtitle ──────────────────────────────────────────────────────
            MakeTMP(root, "Subtitle", "DATA HEIST  //  CLASSIFIED OPERATION", 28,
                TextDim, TextAlignmentOptions.Center,
                new Vector2(0.15f, 0.47f), new Vector2(0.85f, 0.55f));

            // ── Mission briefing block ────────────────────────────────────────
            MakeTMP(root, "Brief",
                "TARGET: CORPORATE DATA CLUSTER  |  THREAT LEVEL: CRITICAL\n" +
                "INFILTRATE. SIPHON. PURGE. EXTRACT.\n" +
                "DO NOT LET THE TRACE METER REACH 100%.",
                22, new Color(0.3f, 0.4f, 0.5f, 1f), TextAlignmentOptions.Center,
                new Vector2(0.2f, 0.37f), new Vector2(0.8f, 0.47f));

            // ── Buttons ───────────────────────────────────────────────────────
            var startBtn = MakeButton(root, "StartButton", "[ INITIALIZE ]",
                new Vector2(960f, 265f), new Vector2(420f, 68f));
            var quitBtn  = MakeButton(root, "QuitButton",  "[ TERMINATE  ]",
                new Vector2(960f, 180f), new Vector2(420f, 68f));

            // ── Version text (bottom-left) ────────────────────────────────────
            MakeTMP(root, "Version", "v0.1  //  CYBER-PULSE", 18,
                new Color(0.25f, 0.3f, 0.4f, 1f), TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0.3f, 0.05f));

            // ── Wire controller ───────────────────────────────────────────────
            var ctrlSO = new SerializedObject(ctrl);
            ctrlSO.FindProperty("_fadePanel").objectReferenceValue  = fade;
            ctrlSO.FindProperty("_titleText").objectReferenceValue  = titleTMP;
            ctrlSO.ApplyModifiedProperties();

            // Wire button listeners via persistent UnityEvent
            WireButtonCallback(startBtn, ctrl, nameof(MainMenuController.OnStartGame));
            WireButtonCallback(quitBtn,  ctrl, nameof(MainMenuController.OnQuit));

            // Fade panel must render last (above everything)
            fadeGO.transform.SetAsLastSibling();
        }

        // ── EventSystem ───────────────────────────────────────────────────────

        private static void AddEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        // ── UI Helpers ────────────────────────────────────────────────────────

        private static TextMeshProUGUI MakeTMP(RectTransform parent, string name,
            string text, float size, Color color, TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.color     = color;
            tmp.alignment = align;
            var rt = tmp.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return tmp;
        }

        private static Button MakeButton(RectTransform parent, string name,
            string label, Vector2 anchoredPos, Vector2 size)
        {
            // Background panel
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var bg  = go.AddComponent<Image>();
            bg.color = BtnNorm;
            var rt  = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos - new Vector2(0f, 0f);
            rt.sizeDelta        = size;
            rt.anchoredPosition = new Vector2(0f, anchoredPos.y - 1080f * 0f);

            // Use a simpler positioning approach
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos - new Vector2(960f, 540f); // offset from centre
            rt.sizeDelta = size;

            var btn = go.AddComponent<Button>();

            // Colour transition
            var colors = btn.colors;
            colors.normalColor      = BtnNorm;
            colors.highlightedColor = new Color(0.05f, 0.12f, 0.22f, 1f);
            colors.pressedColor     = new Color(0f, 0.5f, 0.6f, 1f);
            colors.selectedColor    = colors.highlightedColor;
            btn.colors = colors;

            // Label text
            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lbl = lblGO.AddComponent<TextMeshProUGUI>();
            lbl.text      = label;
            lbl.fontSize  = 38;
            lbl.color     = CyanHDR;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.fontStyle = FontStyles.Bold;
            var lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            return btn;
        }

        private static void WireButtonCallback(Button btn, MainMenuController ctrl, string methodName)
        {
            var so  = new SerializedObject(btn);
            var onClick = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            onClick.arraySize = 1;
            var call = onClick.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = ctrl;
            call.FindPropertyRelative("m_MethodName").stringValue      = methodName;
            call.FindPropertyRelative("m_Mode").intValue               = 1; // void, no args
            call.FindPropertyRelative("m_CallState").intValue          = 2; // Runtime only
            so.ApplyModifiedProperties();
        }

        // ── Utilities (shared with PlayableLevelBuilder) ──────────────────────

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
        }

        private static void TryEnablePostProcessing(GameObject cameraGO)
        {
            var urpType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, " +
                "Unity.RenderPipelines.Universal.Runtime");
            if (urpType == null) return;
            var existing = cameraGO.GetComponent(urpType);
            var data     = existing != null ? existing : cameraGO.AddComponent(urpType);
            urpType.GetProperty("renderPostProcessing")?.SetValue(data, true);
        }
    }
}
