using System;
using System.IO;
using UnityEngine;

namespace MyGame
{
    public enum ReadResult
    {
        Ok,
        Missing,
        Corrupt,
    }

    /// <summary>
    /// JSON file IO with safe writes (spec 6): write to .tmp, replace the target, keep the previous file as .bak.
    /// </summary>
    public static class JsonFileStore
    {
        public static string BackupPath(string path) => path + ".bak";

        public static ReadResult TryRead<T>(string path, out T value) where T : class
        {
            value = null;
            if (!File.Exists(path))
                return ReadResult.Missing;
            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return ReadResult.Corrupt;
                value = JsonUtility.FromJson<T>(json);
                return value != null ? ReadResult.Ok : ReadResult.Corrupt;
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[JsonFileStore] Failed to read '{path}': {e.Message}");
                value = null;
                return ReadResult.Corrupt;
            }
        }

        public static bool TryWrite(string path, object data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonUtility.ToJson(data, true);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                    File.Replace(tmp, path, BackupPath(path));
                else
                    File.Move(tmp, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                Debug.LogError($"[JsonFileStore] Failed to write '{path}': {e.Message}");
                return false;
            }
        }

        public static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[JsonFileStore] Failed to delete '{path}': {e.Message}");
            }
        }
    }
}
