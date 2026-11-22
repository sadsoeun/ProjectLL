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
            int columns = texture.width / tileSize;
            return new Rect((float)(tile % columns * tileSize + .5f) / texture.width,
                (float)(tile / columns * tileSize + .5f) / texture.height,
                (float)(tileSize - 1) / texture.width, (float)(tileSize - 1) / texture.height);
        }
    }
}
