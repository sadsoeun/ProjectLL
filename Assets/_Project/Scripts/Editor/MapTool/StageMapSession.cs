using System;
using System.Collections.Generic;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Window state that is not part of the layout and not under Undo (file, saved snapshot, selection, tool options).
    /// Serialized by the owning EditorWindow so it survives script reloads and Play Mode entry.
    /// </summary>
    [Serializable]
    internal sealed class StageMapSession
    {
        public const float DefaultGrid = 0.5f;

        // Document state
        public bool hasDocument;
        public string filePath = "";          // project-relative, forward slashes
        public string savedJson = "";         // canonical JSON at the last load / save (dirty = current != saved)
        public string currentJson = "";       // canonical JSON of the current content, kept for recovery after a reload
        public string savedFileHash = "";     // hash of the file bytes at the last load / save ("" = file did not exist)
        public bool isDirty;
        public List<string> unknownKeys = new List<string>(); // fields in the loaded file the contract does not define (dropped on save)
        public List<string> loadReport = new List<string>();

        // Selection and tools
        public LayoutElementRef selection;
        public MapPaletteTool tool = MapPaletteTool.Select;
        public string propId = "";            // prop the decoration tool places (PropTable propId)

        // Options
        public float grid = DefaultGrid;
        public bool snap = true;
        public bool surfaceSnap = true;
        public bool showLabels = true;
        public bool showSections = true;
        public bool editEnabled = true;
        public bool autoValidate = true;

        public List<string> recentFiles = new List<string>();
    }
}
