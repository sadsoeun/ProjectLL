using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MyGame
{
    /// <summary>One row of Data/Tables/PropTable.csv. Field names match the CSV header (Docs/prop-table-schema.md).</summary>
    [Serializable]
    public sealed class PropRecord
    {
        public string propId;
        public string name;
        public string category;
        public string prefabPath;
        public float defaultZ;
        public bool collision;
        public string status;
        public string description;

        public bool IsImplemented => status == PropTable.StatusImplemented;
    }

    /// <summary>
    /// Parser for the prop catalog CSV (RFC 4180: quoted fields, "" escapes; UTF-8, BOM tolerated). Reports format errors
    /// (header, column count, bad numbers including NaN / Infinity, bad booleans, unknown category / status, duplicate
    /// propId). Whether a prefab exists is checked by the editor tools, not here.
    /// </summary>
    public sealed class PropTable
    {
        public const string StatusImplemented = "Implemented";
        public const string StatusPlanned = "Planned";

        public const string CategoryTerrain = "Terrain";
        public const string CategoryVegetation = "Vegetation";
        public const string CategoryProp = "Prop";
        public const string CategoryLandmark = "Landmark";
        public const string CategoryGameplay = "Gameplay";
        public const string CategoryBackground = "Background";

        /// <summary>Largest accepted |defaultZ| in metres (a typo guard, not a design limit).</summary>
        public const float MaxAbsZ = 1000f;

        public static readonly string[] Categories =
        {
            CategoryTerrain, CategoryVegetation, CategoryProp, CategoryLandmark, CategoryGameplay, CategoryBackground,
        };

        /// <summary>Column order of the contract. The file must have exactly these headers in this order.</summary>
        public static readonly string[] Columns =
        {
            "propId", "name", "category", "prefabPath", "defaultZ", "collision", "status", "description",
        };

        private readonly List<PropRecord> records = new List<PropRecord>();
        private readonly Dictionary<string, PropRecord> byId = new Dictionary<string, PropRecord>(StringComparer.Ordinal);

        public IReadOnlyList<PropRecord> Records => records;

        public bool TryGet(string propId, out PropRecord record)
        {
            if (propId == null)
            {
                record = null;
                return false;
            }
            return byId.TryGetValue(propId, out record);
        }

        /// <summary>Letters, digits, '_' and '-' only (the id doubles as a file name stem).</summary>
        public static bool IsValidPropId(string id)
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

        /// <summary>Parses the CSV text. Rows with errors are skipped and reported in <paramref name="errors"/>.</summary>
        public static PropTable Parse(string text, List<string> errors)
        {
            var table = new PropTable();
            var rows = SplitRows((text ?? "").TrimStart('﻿'), errors);
            if (rows.Count == 0)
            {
                errors.Add("Table is empty (no header).");
                return table;
            }

            var header = rows[0].Fields;
            if (header.Count != Columns.Length)
                errors.Add($"Header has {header.Count} columns, expected {Columns.Length}.");
            for (int c = 0; c < Math.Min(header.Count, Columns.Length); c++)
            {
                if (header[c].Trim() != Columns[c])
                    errors.Add($"Header column {c + 1} is '{header[c]}', expected '{Columns[c]}'.");
            }
            if (errors.Count > 0)
                return table;

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Fields.Count == 1 && row.Fields[0].Trim().Length == 0)
                    continue; // blank line
                string where = $"line {row.Line}";
                if (row.Fields.Count != Columns.Length)
                {
                    errors.Add($"{where}: {row.Fields.Count} columns, expected {Columns.Length}.");
                    continue;
                }
                int before = errors.Count;
                var rec = ToRecord(row.Fields, where, errors);
                if (errors.Count > before)
                    continue;
                if (table.byId.ContainsKey(rec.propId))
                {
                    errors.Add($"{where}: duplicate propId '{rec.propId}' (primary key must be unique).");
                    continue;
                }
                table.records.Add(rec);
                table.byId.Add(rec.propId, rec);
            }
            return table;
        }

        private static PropRecord ToRecord(List<string> f, string where, List<string> errors)
        {
            int i = 0;
            var r = new PropRecord();
            r.propId = f[i++].Trim();
            if (!IsValidPropId(r.propId))
                errors.Add($"{where}: propId '{r.propId}' must be non-empty and use only letters, digits, '_' and '-'.");
            r.name = f[i++].Trim();
            if (r.name.Length == 0)
                errors.Add($"{where}: name is empty.");
            r.category = f[i++].Trim();
            if (Array.IndexOf(Categories, r.category) < 0)
                errors.Add($"{where}: category '{r.category}' must be one of {string.Join("|", Categories)}.");
            r.prefabPath = f[i++].Trim();
            if (!r.prefabPath.StartsWith("Assets/", StringComparison.Ordinal) || !r.prefabPath.EndsWith(".prefab", StringComparison.Ordinal))
                errors.Add($"{where}: prefabPath '{r.prefabPath}' must be an Assets/... .prefab path.");
            r.defaultZ = Float(f[i++], "defaultZ");
            if (Math.Abs(r.defaultZ) > MaxAbsZ)
                errors.Add($"{where}: defaultZ {r.defaultZ.ToString(CultureInfo.InvariantCulture)} is outside -{MaxAbsZ}..{MaxAbsZ}.");
            r.collision = Bool(f[i++], "collision");
            r.status = f[i++].Trim();
            if (r.status != StatusImplemented && r.status != StatusPlanned)
                errors.Add($"{where}: status '{r.status}' must be {StatusImplemented} or {StatusPlanned}.");
            r.description = f[i].Trim();
            return r;

            float Float(string s, string col)
            {
                if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                    && !float.IsNaN(v) && !float.IsInfinity(v))
                    return v;
                errors.Add($"{where}: {col} '{s}' is not a finite number (use '.' as the decimal point).");
                return 0f;
            }

            bool Bool(string s, string col)
            {
                string t = s.Trim();
                if (t == "true") return true;
                if (t == "false") return false;
                errors.Add($"{where}: {col} '{s}' must be lowercase true or false.");
                return false;
            }
        }

        private struct Row
        {
            public int Line;
            public List<string> Fields;
        }

        /// <summary>RFC 4180 splitter: commas, quoted fields with "" escapes and embedded line breaks; CRLF or LF.</summary>
        private static List<Row> SplitRows(string text, List<string> errors)
        {
            var rows = new List<Row>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            int line = 1, rowLine = 1;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        if (c == '\n') line++;
                        field.Append(c);
                    }
                    continue;
                }

                switch (c)
                {
                    case '"' when field.Length == 0:
                        quoted = true;
                        break;
                    case ',':
                        fields.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        fields.Add(field.ToString());
                        field.Clear();
                        rows.Add(new Row { Line = rowLine, Fields = fields });
                        fields = new List<string>();
                        line++;
                        rowLine = line;
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            if (quoted)
                errors.Add($"line {rowLine}: unterminated quoted field.");
            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                rows.Add(new Row { Line = rowLine, Fields = fields });
            }
            return rows;
        }
    }
}
