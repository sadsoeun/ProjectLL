using System;
using System.Collections.Generic;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Caches the Implemented rows of the monster table for the enemy monsterId picker (MT-012). Read only: the table
    /// is loaded through MonsterTableTool.Load and never written. A failed load is remembered with its reason.
    /// </summary>
    internal sealed class MonsterChoiceCache
    {
        public struct Choice
        {
            public int Id;
            public string Label; // e.g. "1001 보라 순찰병"
        }

        private readonly List<Choice> implemented = new List<Choice>();
        private readonly Dictionary<int, string> statusById = new Dictionary<int, string>();

        public bool Available { get; private set; }
        public string FailureReason { get; private set; }
        public IReadOnlyList<Choice> Implemented => implemented;

        /// <summary>Reloads the table. Never throws; sets Available / FailureReason.</summary>
        public void Refresh()
        {
            implemented.Clear();
            statusById.Clear();
            Available = false;
            FailureReason = null;
            try
            {
                var table = MonsterTableTool.Load(new List<string>());
                foreach (var rec in table.Records)
                {
                    statusById[rec.id] = rec.status;
                    if (rec.IsImplemented)
                        implemented.Add(new Choice { Id = rec.id, Label = $"{rec.id} {rec.devName}" + (rec.movement == "fly" || rec.movement == "burrow" ? $" [{rec.movement}]" : "") });
                }
                Available = true;
            }
            catch (Exception e)
            {
                FailureReason = e.Message;
            }
        }

        /// <summary>Display text for a stored monsterId (0 shows the default). Non-Implemented ids are kept visible as "(Planned) 1003".</summary>
        public string LabelFor(int monsterId)
        {
            if (monsterId == 0)
                return $"기본 (0 → {LayoutEnemy.DefaultMonsterId})";
            foreach (var c in implemented)
            {
                if (c.Id == monsterId)
                    return c.Label;
            }
            string status = statusById.TryGetValue(monsterId, out var s) ? s : "Unknown";
            return $"({status}) {monsterId}";
        }
    }
}
