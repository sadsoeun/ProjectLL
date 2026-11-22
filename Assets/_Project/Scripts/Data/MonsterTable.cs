using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MyGame
{
    /// <summary>One row of Data/Tables/MonsterTable.csv. Field names match the CSV header (Docs/monster-table-schema.md).</summary>
    [Serializable]
    public sealed class MonsterRecord
    {
        public int id;
        public string status;
        public string devName;
        public string nameKey;
        public string prefabPath;
        public string iconPath;
        public string aiGraphPath;
        public int hp;
        public float moveSpeed;
        public float chaseSpeed;
        public float patrolRange;
        public float viewDistance;
        public float viewHalfAngle;
        public float loseDistance;
        public bool canJump;
        public float jumpSpeed;
        public float jumpInterval;
        public bool canAttack;
        public float attackRange;
        public float attackInterval;
        public int attackDamage;
        public bool stompable;
        public bool contactKills;
        public int rewardCoins;
        /// <summary>ground (default) / fly / burrow. See MonsterMovement.</summary>
        public string movement;
        /// <summary>none (default) / lunge / charge / leap / melee / slam / shoot. See MonsterAttack (needs canAttack).</summary>
        public string attack;
        public string devNote;

        public bool IsImplemented => status == MonsterTable.StatusImplemented;

        /// <summary>Parsed movement column; false if it is not ground / fly / burrow (a blank cell means ground).</summary>
        public bool TryGetMovement(out MonsterMovement value) => MonsterTable.TryParseMovement(movement, out value);

        /// <summary>Parsed attack column; false if it is not a known attack (a blank cell means none).</summary>
        public bool TryGetAttack(out MonsterAttack value) => MonsterTable.TryParseAttack(attack, out value);
    }

    /// <summary>
    /// Parser for the monster table CSV (RFC 4180: quoted fields, "" escapes; UTF-8, BOM tolerated).
    /// Parsing reports format errors (unknown/missing columns, bad numbers, duplicate ids); the content rules
    /// (ranges, consistency, asset existence) are checked by the editor tool.
    /// </summary>
    public sealed class MonsterTable
    {
        public const string StatusImplemented = "Implemented";
        public const string StatusPlanned = "Planned";
        public const string StatusDeprecated = "Deprecated";

        /// <summary>Column order of the contract. The file must have exactly these headers in this order.</summary>
        public static readonly string[] Columns =
        {
            "id", "status", "devName", "nameKey", "prefabPath", "iconPath", "aiGraphPath", "hp", "moveSpeed",
            "chaseSpeed", "patrolRange", "viewDistance", "viewHalfAngle", "loseDistance", "canJump", "jumpSpeed",
            "jumpInterval", "canAttack", "attackRange", "attackInterval", "attackDamage", "stompable", "contactKills",
            "rewardCoins", "movement", "attack", "devNote",
        };

        private readonly List<MonsterRecord> records = new List<MonsterRecord>();
        private readonly Dictionary<int, MonsterRecord> byId = new Dictionary<int, MonsterRecord>();

        public IReadOnlyList<MonsterRecord> Records => records;

        public static bool TryParseMovement(string text, out MonsterMovement value)
        {
            switch ((text ?? "").Trim())
            {
                case "":
                case "ground": value = MonsterMovement.Ground; return true;
                case "fly": value = MonsterMovement.Fly; return true;
                case "burrow": value = MonsterMovement.Burrow; return true;
                default: value = MonsterMovement.Ground; return false;
            }
        }

        public static bool TryParseAttack(string text, out MonsterAttack value)
        {
            switch ((text ?? "").Trim())
            {
                case "":
                case "none": value = MonsterAttack.None; return true;
                case "lunge": value = MonsterAttack.Lunge; return true;
                case "charge": value = MonsterAttack.Charge; return true;
                case "leap": value = MonsterAttack.Leap; return true;
                case "melee": value = MonsterAttack.Melee; return true;
                case "slam": value = MonsterAttack.Slam; return true;
                case "shoot": value = MonsterAttack.Shoot; return true;
                default: value = MonsterAttack.None; return false;
            }
        }

        public bool TryGet(int id, out MonsterRecord record) => byId.TryGetValue(id, out record);

        /// <summary>Parses the CSV text. Rows with errors are skipped and reported in <paramref name="errors"/>.</summary>
        public static MonsterTable Parse(string text, List<string> errors)
        {
            var table = new MonsterTable();
            var rows = SplitRows(text.TrimStart('﻿'), errors);
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
                if (table.byId.ContainsKey(rec.id))
                {
                    errors.Add($"{where}: duplicate id {rec.id} (primary key must be unique).");
                    continue;
                }
                table.records.Add(rec);
                table.byId.Add(rec.id, rec);
            }
            return table;
        }

        private static MonsterRecord ToRecord(List<string> f, string where, List<string> errors)
        {
            int i = 0;
            var r = new MonsterRecord();
            r.id = Int(f[i++], "id");
            r.status = f[i++].Trim();
            r.devName = f[i++].Trim();
            r.nameKey = f[i++].Trim();
            r.prefabPath = f[i++].Trim();
            r.iconPath = f[i++].Trim();
            r.aiGraphPath = f[i++].Trim();
            r.hp = Int(f[i++], "hp");
            r.moveSpeed = Float(f[i++], "moveSpeed");
            r.chaseSpeed = Float(f[i++], "chaseSpeed");
            r.patrolRange = Float(f[i++], "patrolRange");
            r.viewDistance = Float(f[i++], "viewDistance");
            r.viewHalfAngle = Float(f[i++], "viewHalfAngle");
            r.loseDistance = Float(f[i++], "loseDistance");
            r.canJump = Bool(f[i++], "canJump");
            r.jumpSpeed = Float(f[i++], "jumpSpeed");
            r.jumpInterval = Float(f[i++], "jumpInterval");
            r.canAttack = Bool(f[i++], "canAttack");
            r.attackRange = Float(f[i++], "attackRange");
            r.attackInterval = Float(f[i++], "attackInterval");
            r.attackDamage = Int(f[i++], "attackDamage");
            r.stompable = Bool(f[i++], "stompable");
            r.contactKills = Bool(f[i++], "contactKills");
            r.rewardCoins = Int(f[i++], "rewardCoins");
            r.movement = f[i++].Trim();
            r.attack = f[i++].Trim();
            r.devNote = f[i].Trim();
            return r;

            int Int(string s, string col)
            {
                if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                    return v;
                errors.Add($"{where}: {col} '{s}' is not an integer.");
                return 0;
            }

            float Float(string s, string col)
            {
                if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    return v;
                errors.Add($"{where}: {col} '{s}' is not a number (use '.' as the decimal point).");
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
