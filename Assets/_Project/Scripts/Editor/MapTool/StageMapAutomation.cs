using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Entry points for tools that cannot use the GUI (the AI level designer via Coplay MCP, batch scripts). All paths are
    /// project-relative ("Assets/_Project/Data/StageLayouts/W1_S1.json"). Failures are returned as a "FAIL ..." line instead
    /// of an exception. Documented in Docs/map-tool-usage.md.
    /// </summary>
    public static class StageMapAutomation
    {
        /// <summary>First line "PASS" or "FAIL", then one ERROR / WARN line per validator message, then INFO lines. Never changes the file.</summary>
        public static string ValidateFile(string path)
        {
            try
            {
                if (!TryRead(path, out string raw, out string failure))
                    return "FAIL\nERROR " + failure;
                if (!StageLayoutJson.TryParse(raw, out var data, out string parseError))
                    return "FAIL\nERROR JSON parse error: " + parseError;

                var errors = new List<string>();
                var warnings = new List<string>();
                StageLayoutValidator.Validate(Path.GetFileName(path), raw, data, Path.GetFileNameWithoutExtension(path), errors, warnings);

                var sb = new StringBuilder(errors.Count == 0 ? "PASS" : "FAIL");
                foreach (var e in errors) sb.Append("\nERROR ").Append(e);
                foreach (var w in warnings) sb.Append("\nWARN ").Append(w);
                sb.Append($"\nINFO errors={errors.Count} warnings={warnings.Count}");
                var unknown = StageLayoutJson.FindUnknownKeys(raw);
                if (unknown.Count > 0)
                    sb.Append("\nINFO unknown fields (dropped when saved by the map tool): ").Append(string.Join(", ", unknown));
                sb.Append("\nINFO canonical format: ").Append(StageLayoutJson.IsCanonical(raw) ? "yes" : "no");
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "FAIL\nERROR " + e.Message;
            }
        }

        /// <summary>Rewrites a parseable file in the canonical format (design 8.2). Returns "OK CHANGED ..." / "OK UNCHANGED ..." / "FAIL ...".</summary>
        public static string NormalizeFile(string path)
        {
            try
            {
                if (!TryRead(path, out string raw, out string failure))
                    return "FAIL " + failure;
                if (!StageLayoutJson.TryParse(raw, out var data, out string parseError))
                    return "FAIL JSON parse error: " + parseError;
                var fixes = StageLayoutJson.Normalize(data);
                string canonical = StageLayoutJson.Write(data);
                if (canonical == raw)
                    return "OK UNCHANGED " + path;
                StageMapPaths.WriteCanonical(path, canonical);
                string note = fixes.Count > 0 ? " (corrected: " + string.Join("; ", fixes) + ")" : "";
                return "OK CHANGED " + path + note;
            }
            catch (Exception e)
            {
                return "FAIL " + e.Message;
            }
        }

        /// <summary>Builds the layout into Stage_&lt;stageId&gt;.unity with StageLayoutBuilder.BuildFromFile. "OK" + report, or "FAIL" + the builder's message.</summary>
        public static string Build(string path)
        {
            try
            {
                if (!TryRead(path, out _, out string failure))
                    return "FAIL " + failure;
                return "OK\n" + StageLayoutBuilder.BuildFromFile(path);
            }
            catch (Exception e)
            {
                return "FAIL " + e.Message;
            }
        }

        /// <summary>Builds and enters Play Mode on the layout (registered stages only). Returns immediately: "STARTED" or "FAIL ...".</summary>
        public static string StartPlayTest(string path)
        {
            try
            {
                if (!TryRead(path, out string raw, out string failure))
                    return "FAIL " + failure;
                if (!StageLayoutJson.TryParse(raw, out var data, out string parseError))
                    return "FAIL JSON parse error: " + parseError;
                if (!StageMapPlayTest.CanPlay(data.stageId, out string reason))
                    return "FAIL " + reason;

                var errors = new List<string>();
                StageLayoutValidator.Validate(Path.GetFileName(path), raw, data, Path.GetFileNameWithoutExtension(path), errors, new List<string>());
                if (errors.Count > 0)
                    return $"FAIL {errors.Count} validation error(s): {errors[0]}";

                return StageMapPlayTest.Start(path, out string message) ? "STARTED" : "FAIL " + message;
            }
            catch (Exception e)
            {
                return "FAIL " + e.Message;
            }
        }

        /// <summary>Opens the layout in the Map Tool window. "FAIL dirty" if the window has unsaved edits.</summary>
        public static string OpenInWindow(string path)
        {
            try
            {
                if (!TryRead(path, out _, out string failure))
                    return "FAIL " + failure;
                foreach (var existing in Resources.FindObjectsOfTypeAll<StageMapToolWindow>())
                {
                    if (existing.IsDirty)
                        return "FAIL dirty";
                }
                var window = EditorWindow.GetWindow<StageMapToolWindow>("Map Tool");
                return window.OpenFromAutomation(path) ? "OK " + path : "FAIL " + window.StatusText;
            }
            catch (Exception e)
            {
                return "FAIL " + e.Message;
            }
        }

        /// <summary>
        /// The prop catalog (Data/Tables/PropTable.csv) for deciding which decorations can be placed. First line
        /// "PROPS n (implemented m)" or "FAIL ...", then one line per row:
        /// "propId | category | status | defaultZ | collision | prefabExists | prefabPath", then "THEMES: name, name" (kit assets).
        /// Only Implemented rows with an existing prefab pass layout validation.
        /// </summary>
        public static string ListProps()
        {
            try
            {
                var errors = new List<string>();
                var table = PropCatalog.Load(errors);
                if (table == null || errors.Count > 0)
                    return "FAIL " + string.Join("; ", errors);

                int implemented = 0;
                var sb = new StringBuilder();
                foreach (var p in table.Records)
                {
                    if (p.IsImplemented)
                        implemented++;
                    sb.Append('\n').Append(p.propId).Append(" | ").Append(p.category).Append(" | ").Append(p.status)
                        .Append(" | ").Append(p.defaultZ.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
                        .Append(" | collision ").Append(p.collision ? "true" : "false")
                        .Append(" | prefab ").Append(PropCatalog.PrefabExists(p) ? "yes" : "no")
                        .Append(" | ").Append(p.prefabPath);
                }
                sb.Insert(0, $"PROPS {table.Records.Count} (implemented {implemented})");
                sb.Append("\nTHEMES: ").Append(string.Join(", ", ThemeKitTool.FindThemeNames()));
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "FAIL " + e.Message;
            }
        }

        /// <summary>Runs the same checks as the Tools/MyGame/Map Tool/Run Self Check menu and returns the report (last line is the summary).</summary>
        public static string RunSelfCheck()
        {
            return StageMapSelfCheck.Run();
        }

        private static bool TryRead(string path, out string raw, out string failure)
        {
            raw = null;
            failure = null;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                failure = "path must be project-relative (Assets/...): " + path;
                return false;
            }
            if (!File.Exists(StageMapPaths.ToAbsolute(path)))
            {
                failure = "file not found: " + path;
                return false;
            }
            raw = StageMapPaths.ReadText(path);
            return true;
        }
    }
}
