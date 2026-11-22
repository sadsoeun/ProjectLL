using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>Project-relative path handling and the small file helpers shared by the controller and the automation API.</summary>
    internal static class StageMapPaths
    {
        public const string TestDataDir = "Assets/_Project/Editor/TestData";
        private const string TempFolder = "Temp/MapTool";
        private const int BackupsToKeep = 10;

        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public static string ToAbsolute(string assetPath) => Path.GetFullPath(Path.Combine(ProjectRoot, assetPath));

        /// <summary>Project-relative path with forward slashes, or null if the file is outside the Assets folder.</summary>
        public static string ToAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return null;
            string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
            string root = ProjectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return null;
            string rel = full.Substring(root.Length);
            return rel.StartsWith("Assets/", StringComparison.Ordinal) ? rel : null;
        }

        public static bool IsValidStageId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            foreach (char c in id)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok)
                    return false;
            }
            return true;
        }

        /// <summary>Hash of the file bytes, or "" if the file does not exist.</summary>
        public static string HashOf(string assetPath)
        {
            string abs = ToAbsolute(assetPath);
            if (!File.Exists(abs))
                return "";
            using (var sha = SHA1.Create())
                return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(abs)));
        }

        /// <summary>Reads a UTF-8 text file (a BOM, if present, is dropped).</summary>
        public static string ReadText(string assetPath)
        {
            return File.ReadAllText(ToAbsolute(assetPath), new UTF8Encoding(false));
        }

        /// <summary>
        /// Writes <paramref name="json"/> to <paramref name="assetPath"/> via a temp file (MT-033): the text is written to
        /// Temp/MapTool, read back and parsed, the existing file is copied to Temp/MapTool/Backup, then the temp file replaces
        /// the target and the asset is imported. Throws on any failure before the target is touched.
        /// </summary>
        public static void WriteCanonical(string assetPath, string json)
        {
            string target = ToAbsolute(assetPath);
            string tempDir = Path.Combine(ProjectRoot, TempFolder);
            Directory.CreateDirectory(tempDir);
            string tmp = Path.Combine(tempDir, Path.GetFileName(target) + ".tmp");
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(tmp, json, utf8);

            string back = File.ReadAllText(tmp, utf8);
            string parseError = null;
            if (back != json || !StageLayoutJson.TryParse(back, out _, out parseError))
                throw new IOException("Temp file verification failed" + (parseError != null ? ": " + parseError : "."));

            if (File.Exists(target))
                Backup(target, tempDir);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(tmp, target, true);
            File.Delete(tmp);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void Backup(string target, string tempDir)
        {
            string dir = Path.Combine(tempDir, "Backup");
            Directory.CreateDirectory(dir);
            string stem = Path.GetFileNameWithoutExtension(target);
            string dst = Path.Combine(dir, $"{stem}.{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            File.Copy(target, dst, true);
            var old = Directory.GetFiles(dir, stem + ".*.json");
            Array.Sort(old, StringComparer.Ordinal);
            for (int i = 0; i < old.Length - BackupsToKeep; i++)
                File.Delete(old[i]);
        }
    }
}
