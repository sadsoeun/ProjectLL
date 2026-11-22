using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MyGame.EditorTools
{
    internal enum IssueSeverity
    {
        Error,
        Warning,
    }

    /// <summary>One validator message, with the element it most likely refers to (D-06, best effort).</summary>
    internal struct ValidationIssue
    {
        public IssueSeverity Severity;
        public string Message;        // validator text without the "label: " prefix
        public LayoutElementRef Target; // None if the message could not be tied to an element
    }

    /// <summary>
    /// Adapter around StageLayoutValidator (MT-016, MT-031): serializes the model to the canonical JSON, runs the
    /// existing validator unchanged and turns its strings into issues. No validation rule lives here; the element
    /// lookup only parses the message text for ids / coordinates.
    /// </summary>
    internal static class StageMapValidation
    {
        private static readonly Regex Quoted = new Regex("'([^']+)'", RegexOptions.Compiled);
        private static readonly Regex KindThenId = new Regex(@"\b(?:checkpoint|enemy|spikes|special|faller|mover|block|section|decoration|spikeTrap|switch|gate)\s+([^\s'(]+)", RegexOptions.Compiled);
        private static readonly Regex CoinAt = new Regex(@"^coin at (?:x=(-?[0-9.]+)|\((-?[0-9.]+), (-?[0-9.]+)\))", RegexOptions.Compiled);
        private static readonly Regex SpawnGoal = new Regex(@"^(spawn|goal)\b", RegexOptions.Compiled);
        private static readonly Regex BossMsg = new Regex(@"^boss\b", RegexOptions.Compiled);
        /// <summary>
        /// Validates <paramref name="d"/> as the validator would validate the saved file. <paramref name="label"/> is the
        /// file name shown in messages; <paramref name="expectedStageId"/> the file name stem (null skips that check).
        /// </summary>
        public static List<ValidationIssue> Run(StageLayoutData d, string label, string expectedStageId)
        {
            var issues = new List<ValidationIssue>();
            string json;
            try
            {
                json = StageLayoutJson.Write(d);
            }
            catch (Exception e)
            {
                issues.Add(new ValidationIssue { Severity = IssueSeverity.Error, Message = "검증 실행 실패 (JSON 직렬화): " + e.Message });
                return issues;
            }

            var errors = new List<string>();
            var warnings = new List<string>();
            try
            {
                StageLayoutValidator.Validate(label, json, d, expectedStageId, errors, warnings);
            }
            catch (Exception e)
            {
                issues.Add(new ValidationIssue { Severity = IssueSeverity.Error, Message = "검증 실행 실패: " + e.Message });
                return issues;
            }

            var ids = BuildIdMap(d);
            string prefix = label + ": ";
            foreach (var e in errors)
                issues.Add(Make(IssueSeverity.Error, e, prefix, d, ids));
            foreach (var w in warnings)
                issues.Add(Make(IssueSeverity.Warning, w, prefix, d, ids));
            return issues;
        }

        /// <summary>True if the issue list came from a failed run (serialization / validator exception) rather than from rules.</summary>
        public static bool HasRunFailure(List<ValidationIssue> issues)
        {
            foreach (var i in issues)
            {
                if (i.Message.StartsWith("검증 실행 실패", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static int Count(List<ValidationIssue> issues, IssueSeverity severity)
        {
            int n = 0;
            foreach (var i in issues)
            {
                if (i.Severity == severity)
                    n++;
            }
            return n;
        }

        private static ValidationIssue Make(IssueSeverity severity, string raw, string prefix, StageLayoutData d,
            Dictionary<string, LayoutElementRef> ids)
        {
            string msg = raw.StartsWith(prefix, StringComparison.Ordinal) ? raw.Substring(prefix.Length) : raw;
            return new ValidationIssue { Severity = severity, Message = msg, Target = FindTarget(msg, d, ids) };
        }

        private static Dictionary<string, LayoutElementRef> BuildIdMap(StageLayoutData d)
        {
            var map = new Dictionary<string, LayoutElementRef>();
            void Add<T>(LayoutElementKind kind, T[] items, Func<T, string> id)
            {
                if (items == null) return;
                for (int i = 0; i < items.Length; i++)
                {
                    string key = id(items[i]);
                    if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                        map[key] = new LayoutElementRef(kind, i);
                }
            }
            Add(LayoutElementKind.Section, d.sections, e => e.id);
            Add(LayoutElementKind.Block, d.blocks, e => e.id);
            Add(LayoutElementKind.Checkpoint, d.checkpoints, e => e.id);
            Add(LayoutElementKind.Special, d.specials, e => e.id);
            Add(LayoutElementKind.Spike, d.spikes, e => e.id);
            Add(LayoutElementKind.Mover, d.movers, e => e.id);
            Add(LayoutElementKind.Enemy, d.enemies, e => e.id);
            Add(LayoutElementKind.Faller, d.fallers, e => e.id);
            Add(LayoutElementKind.Decoration, d.decorations, e => e.id);
            Add(LayoutElementKind.SpikeTrap, d.spikeTraps, e => e.id);
            Add(LayoutElementKind.Switch, d.switches, e => e.id);
            Add(LayoutElementKind.Gate, d.gates, e => e.id);
            return map;
        }

        private static LayoutElementRef FindTarget(string msg, StageLayoutData d, Dictionary<string, LayoutElementRef> ids)
        {
            var spawnGoal = SpawnGoal.Match(msg);
            if (spawnGoal.Success)
                return new LayoutElementRef(spawnGoal.Groups[1].Value == "spawn" ? LayoutElementKind.Spawn : LayoutElementKind.Goal, 0);

            if (BossMsg.IsMatch(msg))
                return new LayoutElementRef(LayoutElementKind.Boss, 0);

            var coin = CoinAt.Match(msg);
            if (coin.Success)
                return FindCoin(d, coin);

            foreach (Match m in Quoted.Matches(msg))
            {
                if (TryId(ids, m.Groups[1].Value, out var r))
                    return r;
            }
            var kindId = KindThenId.Match(msg);
            if (kindId.Success && TryId(ids, kindId.Groups[1].Value, out var r2))
                return r2;
            return LayoutElementRef.None;
        }

        private static bool TryId(Dictionary<string, LayoutElementRef> ids, string token, out LayoutElementRef r)
        {
            if (ids.TryGetValue(token, out r))
                return true;
            // Reachability warnings name vertical movers "<id>(low)" / "<id>(high)".
            int paren = token.IndexOf('(');
            if (paren > 0 && ids.TryGetValue(token.Substring(0, paren), out r))
                return true;
            r = LayoutElementRef.None;
            return false;
        }

        // Coins have no id: pick the coin whose coordinates match the message ("x=12.5" or "(12.5, 3)", printed with 0.##).
        private static LayoutElementRef FindCoin(StageLayoutData d, Match m)
        {
            bool hasY = m.Groups[2].Success;
            if (!float.TryParse(hasY ? m.Groups[2].Value : m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
                return LayoutElementRef.None;
            float y = 0f;
            if (hasY && !float.TryParse(m.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                return LayoutElementRef.None;

            int best = -1;
            float bestDist = 0.02f; // messages print 2 decimals
            for (int i = 0; i < (d.coins?.Length ?? 0); i++)
            {
                float dist = Math.Abs(d.coins[i].x - x) + (hasY ? Math.Abs(d.coins[i].y - y) : 0f);
                if (dist <= bestDist + 1e-4f && (best < 0 || dist < bestDist))
                {
                    best = i;
                    bestDist = dist;
                }
            }
            return best >= 0 ? new LayoutElementRef(LayoutElementKind.Coin, best) : LayoutElementRef.None;
        }
    }
}
