using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Web demo build (browser play, e.g. GitHub Pages): the flow scenes plus the first world's stages only, so the
    /// download stays small. Stages of other worlds are not in the build; the world map already refuses to enter a stage
    /// whose scene is missing. Steam rankings and the Voice Studio are off on the web (see SteamRankingBackend, VoiceRecorder).
    /// Menu: Tools/MyGame/Build Web Demo. Batch: -executeMethod MyGame.EditorTools.WebDemoBuild.BuildFromCommandLine
    /// </summary>
    public static class WebDemoBuild
    {
        public const string DefaultOutput = "C:/Users/PC/Downloads/LeafLeapWeb";
        private const string DemoStagePrefix = "Stage_W1_";
        private const int MaxTextureSize = 2048;

        [MenuItem("Tools/MyGame/Build Web Demo")]
        private static void BuildFromMenu() => Debug.Log(Build(DefaultOutput));

        public static void BuildFromCommandLine()
        {
            string result = Build(DefaultOutput);
            Debug.Log("[WebDemoBuild] " + result);
            if (!result.StartsWith("Succeeded"))
                EditorApplication.Exit(1);
        }

        public static string Build(string outputFolder)
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                    continue;
                bool stage = scene.path.Contains("/Stages/");
                if (!stage || System.IO.Path.GetFileName(scene.path).StartsWith(DemoStagePrefix))
                    scenes.Add(scene.path);
            }

            // GitHub Pages cannot send the "Content-Encoding" header: gzip files with the built-in fallback decoder.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.runInBackground = true;
            // The default ("BuildTimes") ships unoptimised code, which runs several times slower in the browser.
            // Not "RuntimeSpeedLTO": its link step alone ran past 20 minutes here, against under 2 without LTO.
            EditorUserBuildSettings.SetPlatformSettings("WebGL", "CodeOptimization", "RuntimeSpeed");
            PlayerSettings.SetIl2CppCompilerConfiguration(UnityEditor.Build.NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);

            int previousMax = EditorUserBuildSettings.overrideMaxTextureSize;
            EditorUserBuildSettings.overrideMaxTextureSize = MaxTextureSize; // build-time cap; importers stay untouched
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = outputFolder,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                });
                var summary = report.summary;
                string text = $"{summary.result} scenes {scenes.Count} errors {summary.totalErrors} size {summary.totalSize / (1024 * 1024)} MB time {summary.totalTime} -> {summary.outputPath}";
                if (summary.result != BuildResult.Succeeded)
                    foreach (var step in report.steps)
                        foreach (var message in step.messages.Where(m => m.type == LogType.Error).Take(10))
                            text += "\n" + message.content;
                return text;
            }
            finally
            {
                EditorUserBuildSettings.overrideMaxTextureSize = previousMax;
            }
        }
    }
}
