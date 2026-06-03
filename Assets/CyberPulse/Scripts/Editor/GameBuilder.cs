using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CyberPulse.EditorTools
{
    /// <summary>
    /// One-click / headless Windows player build. Builds every enabled scene in
    /// the Build Settings list to Build/Windows/CyberPulse.exe.
    /// Run from the menu (CyberPulse ▶ Build Windows EXE) or headless via
    /// -executeMethod CyberPulse.EditorTools.GameBuilder.BuildWindows.
    /// </summary>
    public static class GameBuilder
    {
        private const string OutputDir = "Build/Windows";
        private const string ExeName   = "CyberPulse.exe";

        [MenuItem("CyberPulse/► Rebuild Level + Build Windows EXE")]
        public static void RebuildLevelAndBuildWindows()
        {
            CyberPulse.Editor.PlayableLevelBuilder.Build();  // regenerate PlayableTestLevel.unity
            BuildWindows();
        }

        [MenuItem("CyberPulse/► Build Windows EXE")]
        public static void BuildWindows()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new Exception("[GameBuilder] No enabled scenes in Build Settings — nothing to build.");

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outDir      = Path.Combine(projectRoot, OutputDir);
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, ExeName);

            var options = new BuildPlayerOptions
            {
                scenes           = scenes,
                locationPathName = exePath,
                target           = BuildTarget.StandaloneWindows64,
                targetGroup      = BuildTargetGroup.Standalone,
                options          = BuildOptions.None,
            };

            Debug.Log($"[GameBuilder] Building {scenes.Length} scene(s) → {exePath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[GameBuilder] BUILD SUCCEEDED — {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime}. Output: {exePath}");
            }
            else
            {
                throw new Exception($"[GameBuilder] BUILD {summary.result} — {summary.totalErrors} error(s).");
            }
        }
    }
}
