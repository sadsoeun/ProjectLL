using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Test builds only: Steam needs an App ID to start outside the Steam client, so steam_appid.txt is kept in the
    /// project root (editor play) and copied beside the built executable.
    /// Until the game has its own App ID this is Valve's public test ID 480.
    /// A build uploaded to Steam must NOT ship this file: set <see cref="CopyToBuilds"/> to false (or delete this script) then.
    /// </summary>
    public sealed class SteamTestAppId : IPostprocessBuildWithReport
    {
        private const string FileName = "steam_appid.txt";
        private const string TestAppId = "480";
        private const bool CopyToBuilds = true;

        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void EnsureProjectFile()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), FileName);
            if (!File.Exists(path))
                File.WriteAllText(path, TestAppId);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!CopyToBuilds || report.summary.platform != BuildTarget.StandaloneWindows64)
                return;
            string source = Path.Combine(Directory.GetCurrentDirectory(), FileName);
            string folder = Path.GetDirectoryName(report.summary.outputPath);
            if (!File.Exists(source) || string.IsNullOrEmpty(folder))
                return;
            File.Copy(source, Path.Combine(folder, FileName), true);
            Debug.Log("[Ranking] Copied " + FileName + " (test App ID) beside the build. Remove it for a Steam release.");
        }
    }
}
