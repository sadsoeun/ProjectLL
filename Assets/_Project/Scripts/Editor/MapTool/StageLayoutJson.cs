using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Reading and canonical writing of stage layout JSON (design 8.2). Reading uses the same JsonUtility call as
    /// the builder so both interpret a file identically; writing reproduces the existing hand-maintained format
    /// (1-space indent, fixed key order, "0.0###" numbers) so saving an untouched file yields no diff.
    /// Pure static, no state, no editor dependencies.
    /// </summary>
    internal static class StageLayoutJson
    {
        private static readonly string[] TopLevelKeys =
        {
            "version", "stageId", "length", "killY", "cameraMinY", "spawn", "goal", "sections", "blocks",
            "checkpoints", "coins", "specials", "spikes", "movers", "enemies", "fallers",
        };

        // Optional top-level keys (v1.3 / v1.4). Written only when they carry content, so layouts without them stay byte-identical.
        private static readonly string[] OptionalTopLevelKeys = { "theme", "decorations", "spikeTraps", "switches", "gates", "boss" };

        // Keys valid anywhere below the top level (union over all element types; spawn/goal use x and y).
        private static readonly HashSet<string> ElementKeys = new HashSet<string>
        {
            "id", "section", "kind", "x", "y", "w", "h", "startX", "endX", "note", "slot", "axis", "distance", "speed",
            "wait", "monsterId", "range", "triggerHalfWidth", "delay", "respawn", "prop", "z", "rotY", "scale",
            "period", "upTime", "warn", "offset", "channel", "once", "openSeconds", "bossId", "arenaMinX", "arenaMaxX", "surface",
        };

        private struct Field
        {
            public string Key;
            public string Value; // already rendered JSON value

            public Field(string key, string value)
            {
                Key = key;
                Value = value;
            }
        }

        // ------------------------------------------------------------------ reading

        /// <summary>Parses with JsonUtility (same as the builder). On failure data is null and error says why.</summary>
        public static bool TryParse(string raw, out StageLayoutData data, out string error)
        {
            data = null;
            error = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "file is empty.";
                return false;
            }
            try
            {
                data = JsonUtility.FromJson<StageLayoutData>(raw);
            }
            catch (Exception e)
            {
                error = e.Message;
                data = null;
                return false;
            }
            if (data == null)
            {
                error = "JSON could not be parsed.";
                return false;
            }
            return true;
        }

        /// <summary>Replaces null arrays / points with empty ones. Returns what was corrected (empty = nothing).</summary>
        public static List<string> Normalize(StageLayoutData d)
        {
            Assert.IsNotNull(d, "Normalize needs data.");
            var fixes = new List<string>();
            if (d.spawn == null) { d.spawn = new LayoutPoint(); fixes.Add("spawn was missing (set to 0,0)"); }
            if (d.goal == null) { d.goal = new LayoutPoint(); fixes.Add("goal was missing (set to 0,0)"); }
            Fix(ref d.sections, "sections", fixes);
            Fix(ref d.blocks, "blocks", fixes);
            Fix(ref d.checkpoints, "checkpoints", fixes);
            Fix(ref d.coins, "coins", fixes);
            Fix(ref d.specials, "specials", fixes);
            Fix(ref d.spikes, "spikes", fixes);
            Fix(ref d.movers, "movers", fixes);
            Fix(ref d.enemies, "enemies", fixes);
            Fix(ref d.fallers, "fallers", fixes);
            // Optional array (v1.3): a missing one is normal, so it is not reported as a correction.
            if (d.decorations == null)
                d.decorations = Array.Empty<LayoutDecoration>();
            if (d.spikeTraps == null)
                d.spikeTraps = Array.Empty<LayoutSpikeTrap>();
            if (d.switches == null)
                d.switches = Array.Empty<LayoutSwitch>();
            if (d.gates == null)
                d.gates = Array.Empty<LayoutGate>();
            if (d.boss == null)
                d.boss = new LayoutBoss();
            return fixes;
        }

        private static void Fix<T>(ref T[] array, string name, List<string> fixes)
        {
            if (array != null)
                return;
            array = Array.Empty<T>();
            fixes.Add(name + " was missing (set to empty)");
        }

        /// <summary>Keys of the raw text that the contract does not define ("depth:key" for nested ones).</summary>
        public static IReadOnlyList<string> FindUnknownKeys(string raw)
        {
            var result = new List<string>();
            var top = new HashSet<string>(TopLevelKeys);
            top.UnionWith(OptionalTopLevelKeys);
            foreach (var kv in ScanKeys(raw))
            {
                bool known = kv.Depth == 1 ? top.Contains(kv.Key) : ElementKeys.Contains(kv.Key);
                if (!known && !result.Contains(kv.Key))
                    result.Add(kv.Key);
            }
            return result;
        }

        /// <summary>Contract top-level keys that the raw text does not contain.</summary>
        public static IReadOnlyList<string> FindMissingTopLevelKeys(string raw)
        {
            var present = new HashSet<string>();
            foreach (var kv in ScanKeys(raw))
            {
                if (kv.Depth == 1)
                    present.Add(kv.Key);
            }
            var missing = new List<string>();
            foreach (var k in TopLevelKeys)
            {
                if (!present.Contains(k))
                    missing.Add(k);
            }
            return missing;
        }

        /// <summary>True if the raw text is byte-identical to what Write would produce for its parsed content.</summary>
        public static bool IsCanonical(string raw)
        {
            if (!TryParse(raw, out var data, out _))
                return false;
            Normalize(data);
            try
            {
                return Write(data) == raw;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private struct KeyAt
        {
            public string Key;
            public int Depth; // 1 = a key of the root object
        }

        /// <summary>Lists object keys (strings followed by ':') with their nesting depth, skipping string contents.</summary>
        private static List<KeyAt> ScanKeys(string raw)
        {
            var keys = new List<KeyAt>();
            if (string.IsNullOrEmpty(raw))
                return keys;
            int depth = 0;
            int i = 0;
            var sb = new StringBuilder();
            while (i < raw.Length)
            {
                char c = raw[i];
                if (c == '{' || c == '[') { depth++; i++; }
                else if (c == '}' || c == ']') { depth--; i++; }
                else if (c == '"')
                {
                    sb.Length = 0;
                    i++;
                    while (i < raw.Length && raw[i] != '"')
                    {
                        if (raw[i] == '\\' && i + 1 < raw.Length)
                            i++;
                        sb.Append(raw[i]);
                        i++;
                    }
                    i++; // closing quote
                    int j = i;
                    while (j < raw.Length && char.IsWhiteSpace(raw[j]))
                        j++;
                    if (j < raw.Length && raw[j] == ':')
                        keys.Add(new KeyAt { Key = sb.ToString(), Depth = depth });
                }
                else
                {
                    i++;
                }
            }
            return keys;
        }

        // ------------------------------------------------------------------ writing (design 8.2, W1..W7)

        /// <summary>Formats a float as the canonical number text (W5). Throws InvalidOperationException for NaN / infinity.</summary>
        public static string FormatFloat(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 1e12f)
                throw new InvalidOperationException("Value cannot be written to JSON: " + v.ToString(CultureInfo.InvariantCulture));
            string s = ((decimal)v).ToString("0.0###", CultureInfo.InvariantCulture);
            return s == "-0.0" ? "0.0" : s;
        }

        /// <summary>Canonical JSON text of <paramref name="d"/>. Throws InvalidOperationException if a number cannot be written.</summary>
        public static string Write(StageLayoutData d)
        {
            Assert.IsNotNull(d, "Write needs data.");
            var members = new List<string>(18);
            members.Add(" \"version\": " + d.version.ToString(CultureInfo.InvariantCulture));
            members.Add(" \"stageId\": " + Str(d.stageId));
            members.Add(" \"length\": " + FormatFloat(d.length));
            members.Add(" \"killY\": " + FormatFloat(d.killY));
            members.Add(" \"cameraMinY\": " + FormatFloat(d.cameraMinY));
            members.Add(PointMember("spawn", d.spawn));
            members.Add(PointMember("goal", d.goal));
            members.Add(ArrayMember("sections", d.sections, FillSection));
            members.Add(ArrayMember("blocks", d.blocks, FillBlock));
            members.Add(ArrayMember("checkpoints", d.checkpoints, FillCheckpoint));
            members.Add(ArrayMember("coins", d.coins, FillCoin));
            members.Add(ArrayMember("specials", d.specials, FillSpecial));
            members.Add(ArrayMember("spikes", d.spikes, FillSpike));
            members.Add(ArrayMember("movers", d.movers, FillMover));
            members.Add(ArrayMember("enemies", d.enemies, FillEnemy));
            members.Add(ArrayMember("fallers", d.fallers, FillFaller));
            if (!string.IsNullOrEmpty(d.theme))
                members.Add(" \"theme\": " + Str(d.theme));
            if (d.decorations != null && d.decorations.Length > 0)
                members.Add(ArrayMember("decorations", d.decorations, FillDecoration));
            if (d.spikeTraps != null && d.spikeTraps.Length > 0)
                members.Add(ArrayMember("spikeTraps", d.spikeTraps, FillSpikeTrap));
            if (d.switches != null && d.switches.Length > 0)
                members.Add(ArrayMember("switches", d.switches, FillSwitch));
            if (d.gates != null && d.gates.Length > 0)
                members.Add(ArrayMember("gates", d.gates, FillGate));
            if (d.HasBoss)
                members.Add(BossMember(d.boss));
            return "{\n" + string.Join(",\n", members) + "\n}";
        }

        private static string PointMember(string key, LayoutPoint p)
        {
            var fields = new List<Field>(2)
            {
                new Field("x", FormatFloat(p != null ? p.x : 0f)),
                new Field("y", FormatFloat(p != null ? p.y : 0f)),
            };
            var sb = new StringBuilder();
            sb.Append(" \"").Append(key).Append("\": ");
            AppendObject(sb, 1, fields);
            return sb.ToString();
        }

        private static string ArrayMember<T>(string key, T[] items, Action<T, List<Field>> fill)
        {
            var sb = new StringBuilder();
            sb.Append(" \"").Append(key).Append("\": ");
            if (items == null || items.Length == 0)
            {
                sb.Append("[]");
                return sb.ToString();
            }
            sb.Append("[\n");
            var fields = new List<Field>(10);
            for (int i = 0; i < items.Length; i++)
            {
                fields.Clear();
                fill(items[i], fields);
                sb.Append("  ");
                AppendObject(sb, 2, fields);
                sb.Append(i < items.Length - 1 ? ",\n" : "\n");
            }
            sb.Append(" ]");
            return sb.ToString();
        }

        /// <summary>Writes "{", the fields one per line at depth+1 and "}" at <paramref name="depth"/> (opening brace not indented).</summary>
        private static void AppendObject(StringBuilder sb, int depth, List<Field> fields)
        {
            sb.Append("{\n");
            for (int i = 0; i < fields.Count; i++)
            {
                sb.Append(' ', depth + 1).Append('"').Append(fields[i].Key).Append("\": ").Append(fields[i].Value);
                sb.Append(i < fields.Count - 1 ? ",\n" : "\n");
            }
            sb.Append(' ', depth).Append('}');
        }

        private static Field F(string key, float v) => new Field(key, FormatFloat(v));
        private static Field I(string key, int v) => new Field(key, v.ToString(CultureInfo.InvariantCulture));
        private static Field S(string key, string v) => new Field(key, Str(v));

        private static void OptSection(List<Field> f, string section)
        {
            if (!string.IsNullOrEmpty(section))
                f.Add(S("section", section));
        }

        private static void FillSection(LayoutSection s, List<Field> f)
        {
            f.Add(S("id", s.id)); f.Add(F("startX", s.startX)); f.Add(F("endX", s.endX)); f.Add(S("note", s.note));
        }

        private static void FillBlock(LayoutBlock b, List<Field> f)
        {
            f.Add(S("id", b.id)); OptSection(f, b.section); f.Add(S("kind", b.kind));
            f.Add(F("x", b.x)); f.Add(F("y", b.y)); f.Add(F("w", b.w)); f.Add(F("h", b.h));
            if (!string.IsNullOrEmpty(b.surface)) f.Add(S("surface", b.surface));
        }

        private static void FillCheckpoint(LayoutCheckpoint c, List<Field> f)
        {
            f.Add(S("id", c.id)); OptSection(f, c.section); f.Add(F("x", c.x)); f.Add(F("y", c.y));
        }

        private static void FillCoin(LayoutCoin c, List<Field> f)
        {
            OptSection(f, c.section); f.Add(F("x", c.x)); f.Add(F("y", c.y));
        }

        private static void FillSpecial(LayoutSpecial s, List<Field> f)
        {
            f.Add(S("id", s.id)); OptSection(f, s.section); f.Add(I("slot", s.slot)); f.Add(F("x", s.x)); f.Add(F("y", s.y));
        }

        private static void FillSpike(LayoutSpike s, List<Field> f)
        {
            f.Add(S("id", s.id)); OptSection(f, s.section); f.Add(F("x", s.x)); f.Add(F("y", s.y)); f.Add(F("w", s.w));
        }

        private static void FillMover(LayoutMover m, List<Field> f)
        {
            f.Add(S("id", m.id)); OptSection(f, m.section);
            f.Add(F("x", m.x)); f.Add(F("y", m.y)); f.Add(F("w", m.w)); f.Add(S("axis", m.axis));
            f.Add(F("distance", m.distance)); f.Add(F("speed", m.speed)); f.Add(F("wait", m.wait));
        }

        private static void FillEnemy(LayoutEnemy e, List<Field> f)
        {
            f.Add(S("id", e.id)); OptSection(f, e.section);
            if (e.monsterId != 0)
                f.Add(I("monsterId", e.monsterId));
            f.Add(F("x", e.x)); f.Add(F("y", e.y)); f.Add(F("range", e.range));
            if (e.speed != 0f)
                f.Add(F("speed", e.speed));
        }

        private static void FillFaller(LayoutFaller l, List<Field> f)
        {
            f.Add(S("id", l.id)); OptSection(f, l.section);
            f.Add(F("x", l.x)); f.Add(F("y", l.y)); f.Add(F("triggerHalfWidth", l.triggerHalfWidth));
            f.Add(F("delay", l.delay)); f.Add(F("respawn", l.respawn));
        }

        private static void FillSpikeTrap(LayoutSpikeTrap t, List<Field> f)
        {
            f.Add(S("id", t.id)); OptSection(f, t.section);
            f.Add(F("x", t.x)); f.Add(F("y", t.y)); f.Add(F("w", t.w));
            f.Add(F("period", t.period)); f.Add(F("upTime", t.upTime)); f.Add(F("warn", t.warn)); f.Add(F("offset", t.offset));
        }

        private static void FillSwitch(LayoutSwitch s, List<Field> f)
        {
            f.Add(S("id", s.id)); OptSection(f, s.section);
            f.Add(F("x", s.x)); f.Add(F("y", s.y)); f.Add(I("channel", s.channel));
            f.Add(new Field("once", s.once ? "true" : "false"));
        }

        private static void FillGate(LayoutGate g, List<Field> f)
        {
            f.Add(S("id", g.id)); OptSection(f, g.section);
            f.Add(F("x", g.x)); f.Add(F("y", g.y)); f.Add(F("h", g.h));
            f.Add(I("channel", g.channel)); f.Add(F("openSeconds", g.openSeconds));
        }

        private static string BossMember(LayoutBoss b)
        {
            var fields = new List<Field>(5)
            {
                I("bossId", b.bossId), F("x", b.x), F("y", b.y), F("arenaMinX", b.arenaMinX), F("arenaMaxX", b.arenaMaxX),
            };
            var sb = new StringBuilder();
            sb.Append(" \"boss\": ");
            AppendObject(sb, 1, fields);
            return sb.ToString();
        }

        private static void FillDecoration(LayoutDecoration e, List<Field> f)
        {
            f.Add(S("id", e.id)); f.Add(S("prop", e.prop));
            f.Add(F("x", e.x)); f.Add(F("y", e.y));
            if (e.HasZ)
                f.Add(F("z", e.z)); // omitted = the prop table's defaultZ
            f.Add(F("rotY", e.rotY)); f.Add(F("scale", e.scale));
        }

        /// <summary>Quoted JSON string (W6): escapes " \ and control characters; non-ASCII is written as is.</summary>
        private static string Str(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "\"\"";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
