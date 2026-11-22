using System;
using UnityEngine;

namespace MyGame
{
    /// <summary>Turntable views of the creatures actually placed in the playable stage scenes.</summary>
    public sealed class WorldMapEncounterAtlas : ScriptableObject
    {
        [Serializable]
        public class Stage
        {
            public string stageId;
            public int[] models;
        }

        /// <summary>Tiles per sheet row (the baker lays the sheet out with this).</summary>
        public const int Columns = 16;

        public Texture2D texture;
        public int viewCount;
        public int tileSize;
        public int[] creatureIds;
        public Stage[] stages;

        public int[] ModelsFor(string stageId)
        {
            foreach (var stage in stages)
                if (stage.stageId == stageId) return stage.models;
            return Array.Empty<int>();
        }

        public Rect UV(int model, int view)
        {
            int tile = 1 + model * viewCount + view; // The frame lines sample white in tile zero.
            // Grid cells, not texture pixels: a build may ship the sheet smaller than it was baked (web demo texture cap).
            int rows = (creatureIds.Length * viewCount + Columns) / Columns; // tiles + the white one, rounded up
            float inset = .5f / tileSize;
            return new Rect((tile % Columns + inset) / Columns, (tile / Columns + inset) / rows,
                (1f - 2f * inset) / Columns, (1f - 2f * inset) / rows);
        }
    }
}
