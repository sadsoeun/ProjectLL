using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame.EditorTools
{
    /// <summary>
    /// The single editable copy of a layout. A ScriptableObject only so Unity's Undo can record the serialized
    /// StageLayoutData; it is never saved as an asset (HideFlags.DontSave) and is destroyed by the owning window.
    /// Always re-read <see cref="Data"/>: Undo may replace the contained objects.
    /// </summary>
    internal sealed class StageLayoutDocument : ScriptableObject
    {
        [SerializeField] private StageLayoutData data;

        public StageLayoutData Data => data;

        public static StageLayoutDocument CreateTransient(StageLayoutData initial)
        {
            var doc = CreateInstance<StageLayoutDocument>();
            doc.hideFlags = HideFlags.DontSave;
            doc.name = "StageLayoutDocument";
            doc.data = initial;
            return doc;
        }

        /// <summary>Replaces the whole content without an Undo record (open / new / reload). Callers clear the Undo history.</summary>
        public void Replace(StageLayoutData newData)
        {
            Assert.IsNotNull(newData, "Replace needs data.");
            data = newData;
            AssertInvariant();
        }

        /// <summary>Object invariants (programming errors if broken): arrays and points are non-null.</summary>
        public void AssertInvariant()
        {
            Assert.IsNotNull(data, "document data is null");
            if (data == null)
                return;
            Assert.IsNotNull(data.spawn, "spawn is null");
            Assert.IsNotNull(data.goal, "goal is null");
            Assert.IsNotNull(data.sections, "sections is null");
            Assert.IsNotNull(data.blocks, "blocks is null");
            Assert.IsNotNull(data.checkpoints, "checkpoints is null");
            Assert.IsNotNull(data.coins, "coins is null");
            Assert.IsNotNull(data.specials, "specials is null");
            Assert.IsNotNull(data.spikes, "spikes is null");
            Assert.IsNotNull(data.movers, "movers is null");
            Assert.IsNotNull(data.enemies, "enemies is null");
            Assert.IsNotNull(data.fallers, "fallers is null");
            Assert.IsNotNull(data.decorations, "decorations is null");
            Assert.IsNotNull(data.spikeTraps, "spikeTraps is null");
            Assert.IsNotNull(data.switches, "switches is null");
            Assert.IsNotNull(data.gates, "gates is null");
            Assert.IsNotNull(data.boss, "boss is null");
        }
    }
}
