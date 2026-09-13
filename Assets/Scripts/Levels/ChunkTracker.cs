using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.Scripts.Levels
{
    public class ChunkTracker
    {
        public Dictionary<float2, Chunk> chunkDictionary;

        public List<float2> activeChunkCoords;

        public Transform player;

        public int chunkLength;

        public int XChunkPos => (int)Mathf.Floor(player.position.x / chunkLength);

        public int ZChunkPos => (int)Mathf.Floor(player.position.z / chunkLength);

        public readonly Vector3 origin = Vector3.zero;

        public ChunkTracker(Transform player, int chunkLength)
        {
            chunkDictionary = new();
            activeChunkCoords = new();

            this.player = player;
            this.chunkLength = chunkLength;
        }   
    }
}
