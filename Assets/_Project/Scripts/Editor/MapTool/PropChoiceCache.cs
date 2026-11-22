using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>Loading of Data/Tables/PropTable.csv for the editor tools (validator, builder, map tool).</summary>
    internal static class PropCatalog
    {
        public const string TablePath = "Assets/_Project/Data/Tables/PropTable.csv";

        /// <summary>
        /// Parses the table. Returns null (with an error) when the file is missing; otherwise the parsed table, with format
        /// errors appended to <paramref name="errors"/> (rows with errors are left out). Never throws.
        /// </summary>
        public static PropTable Load(List<string> errors)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath);
            if (asset == null)
            {
                errors.Add("Prop table not found: " + TablePath);
                return null;
            }
            return PropTable.Parse(asset.text, errors);
        }

        /// <summary>True if the prefab asset of a row exists (the asset database knows the path).</summary>
        public static bool PrefabExists(PropRecord rec)
        {
            return rec != null && !string.IsNullOrEmpty(rec.prefabPath) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(rec.prefabPath));
        }
    }

    /// <summary>
    /// Cache for the map tool UI: the Implemented rows grouped by category (decoration picker), default depths and the
    /// XY extent of each prop prefab (overlay). Read only. A failed load is remembered with its reason. Call Refresh when
    /// the table or prefabs may have changed (window focus).
    /// </summary>
    internal sealed class PropChoiceCache
    {
        public struct Choice
        {
            public string PropId;
            public string Category;
            public string Label; // e.g. "V01_BroadleafOak  큰 활엽수"
        }

        private readonly List<Choice> implemented = new List<Choice>();
        private readonly Dictionary<string, PropRecord> byId = new Dictionary<string, PropRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, Rect?> bounds = new Dictionary<string, Rect?>(StringComparer.Ordinal);

        public bool Available { get; private set; }
        public string FailureReason { get; private set; }
        public int TotalRows { get; private set; }
        public IReadOnlyList<Choice> Implemented => implemented;

        /// <summary>Reloads the table. Never throws; sets Available / FailureReason.</summary>
        public void Refresh()
        {
            implemented.Clear();
            byId.Clear();
            bounds.Clear();
            Available = false;
            FailureReason = null;
            TotalRows = 0;
            try
            {
                var errors = new List<string>();
                var table = PropCatalog.Load(errors);
                if (table == null || errors.Count > 0)
                {
                    FailureReason = string.Join("; ", errors);
                    return;
                }
                foreach (var rec in table.Records)
                {
                    byId[rec.propId] = rec;
                    TotalRows++;
                    if (rec.IsImplemented && PropCatalog.PrefabExists(rec))
                        implemented.Add(new Choice { PropId = rec.propId, Category = rec.category, Label = $"{rec.propId}  {rec.name}" });
                }
                Available = true;
            }
            catch (Exception e)
            {
                FailureReason = e.Message;
            }
        }

        public bool TryGet(string propId, out PropRecord rec)
        {
            if (propId == null)
            {
                rec = null;
                return false;
            }
            return byId.TryGetValue(propId, out rec);
        }

        /// <summary>The decoration's depth: its own z, or the prop's defaultZ when z is unset (0 for an unknown prop).</summary>
        public float ResolveZ(LayoutDecoration dec)
        {
            return TryGet(dec.prop, out var rec) ? dec.ResolveZ(rec.defaultZ) : dec.ResolveZ(0f);
        }

        public float DefaultZ(string propId) => TryGet(propId, out var rec) ? rec.defaultZ : 0f;

        /// <summary>Display text for a stored prop id; a non-Implemented or unknown id stays visible as "(Planned) id".</summary>
        public string LabelFor(string propId)
        {
            if (string.IsNullOrEmpty(propId))
                return "(없음)";
            foreach (var c in implemented)
            {
                if (c.PropId == propId)
                    return c.Label;
            }
            string status = TryGet(propId, out var rec) ? (PropCatalog.PrefabExists(rec) || !rec.IsImplemented ? rec.status : "NoPrefab") : "Unknown";
            return $"({status}) {propId}";
        }

        /// <summary>
        /// XY extent of the prop prefab at scale 1 relative to its pivot (from the renderers' mesh bounds), or null if the
        /// prefab is missing or has no mesh. Cached until the next Refresh.
        /// </summary>
        public Rect? LocalBounds(string propId)
        {
            if (string.IsNullOrEmpty(propId))
                return null;
            if (bounds.TryGetValue(propId, out var cached))
                return cached;
            Rect? result = null;
            if (TryGet(propId, out var rec) && PropCatalog.PrefabExists(rec))
                result = ComputeBounds(AssetDatabase.LoadAssetAtPath<GameObject>(rec.prefabPath));
            bounds[propId] = result;
            return result;
        }

        private static Rect? ComputeBounds(GameObject prefab)
        {
            if (prefab == null)
                return null;
            var toRoot = prefab.transform.worldToLocalMatrix;
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;

            void Add(Mesh mesh, Matrix4x4 local)
            {
                if (mesh == null)
                    return;
                Bounds b = mesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = new Vector3(
                        (k & 1) == 0 ? b.min.x : b.max.x,
                        (k & 2) == 0 ? b.min.y : b.max.y,
                        (k & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = local.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        min = max = p;
                        any = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, p);
                        max = Vector3.Max(max, p);
                    }
                }
            }

            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                Add(mf.sharedMesh, toRoot * mf.transform.localToWorldMatrix);
            foreach (var sm in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Add(sm.sharedMesh, toRoot * sm.transform.localToWorldMatrix);

            if (!any || float.IsNaN(min.x + max.x + min.y + max.y) || float.IsInfinity(min.x + max.x + min.y + max.y))
                return null;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
