using System.IO;
using UnityEngine;

namespace MyGame
{
    public enum SaveLoadStatus
    {
        None,                // no save file
        Loaded,              // main file loaded
        RecoveredFromBackup, // main file damaged, .bak used
        Corrupted,           // damaged and unrecoverable (treated as no save)
    }

    /// <summary>Single-slot progress storage (save_01.json).</summary>
    public sealed class SaveService : MonoBehaviour
    {
        public const string FileName = "save_01.json";

        [Tooltip("Lives given on a new game and after a game over.")]
        [SerializeField, Min(1)] private int initialLives = 5;

        public int InitialLives => initialLives;
        /// <summary>Current progress, or null when no valid save is loaded.</summary>
        public SaveData Current { get; private set; }
        public bool HasSave => Current != null;
        /// <summary>True for in-memory test data created when a scene is played directly. Never written to disk.</summary>
        public bool IsTemporary { get; private set; }
        public SaveLoadStatus LastStatus { get; private set; }

        private string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public void Initialize()
        {
            Current = null;
            IsTemporary = false;
            LastStatus = SaveLoadStatus.None;
        }

        /// <summary>Reads the save from disk (with .bak recovery) and replaces Current. Returns true if a valid save exists.</summary>
        public bool Refresh()
        {
            Current = null;
            IsTemporary = false;

            var main = JsonFileStore.TryRead(FilePath, out SaveData data);
            if (main == ReadResult.Ok && TryAccept(data))
            {
                LastStatus = SaveLoadStatus.Loaded;
                return true;
            }

            var bak = JsonFileStore.TryRead(JsonFileStore.BackupPath(FilePath), out SaveData backup);
            if (bak == ReadResult.Ok && TryAccept(backup))
            {
                Debug.LogWarning("[SaveService] Main save damaged; recovered from .bak.");
                LastStatus = SaveLoadStatus.RecoveredFromBackup;
                Save(); // restore the main file
                return true;
            }

            if (main == ReadResult.Missing && bak == ReadResult.Missing)
            {
                LastStatus = SaveLoadStatus.None;
            }
            else
            {
                Debug.LogWarning("[SaveService] Save file is damaged and could not be recovered. Treating as no save.");
                LastStatus = SaveLoadStatus.Corrupted;
            }
            return false;
        }

        /// <summary>True if a save file (or its .bak) exists on disk. Does not read or validate it.</summary>
        public bool HasSaveOnDisk => File.Exists(FilePath) || File.Exists(JsonFileStore.BackupPath(FilePath));

        /// <summary>
        /// Re-reads the save from disk like <see cref="Refresh"/>, but keeps the current progress (and its temporary
        /// flag) when no valid save can be read, so a failed load never wipes the game in memory.
        /// </summary>
        public bool ReloadFromDisk()
        {
            var previous = Current;
            bool wasTemporary = IsTemporary;
            if (Refresh())
                return true;
            Current = previous;
            IsTemporary = wasTemporary;
            return false;
        }

        /// <summary>Creates a fresh save and writes it. Returns false if the write failed.</summary>
        public bool CreateNew(string startNodeId)
        {
            if (string.IsNullOrEmpty(startNodeId))
                throw new System.ArgumentException("startNodeId is empty", nameof(startNodeId));
            Current = SaveData.CreateNew(startNodeId, initialLives);
            IsTemporary = false;
            LastStatus = SaveLoadStatus.Loaded;
            return Save();
        }

        /// <summary>In-memory test save for direct scene play (spec 4.3). Not persisted.</summary>
        public void CreateTemporary(string startNodeId)
        {
            Current = SaveData.CreateNew(startNodeId, initialLives);
            IsTemporary = true;
            Debug.Log("[SaveService] Using temporary in-memory save (not written to disk).");
        }

        public bool Save()
        {
            if (Current == null)
            {
                Debug.LogError("[SaveService] Save called without a loaded save.");
                return false;
            }
            if (IsTemporary)
                return true;
            Current.updatedAt = System.DateTime.UtcNow.ToString("o");
            return JsonFileStore.TryWrite(FilePath, Current);
        }

        public void ResetLives()
        {
            if (Current != null)
                Current.lives = initialLives;
        }

        private bool TryAccept(SaveData data)
        {
            if (data == null || !data.IsValid())
                return false;
            Migrate(data);
            _ = data.Tutorial; // judge pre-tutorial saves now, before any new clear is recorded
            Current = data;
            return true;
        }

        // Hook for older versions (spec 8). Only version 1 exists.
        private static void Migrate(SaveData data)
        {
            data.version = SaveData.CurrentVersion;
        }
    }
}
