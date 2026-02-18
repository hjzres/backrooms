using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Assets.Scripts.Levels
{
    public class LobbyGenerator : MonoBehaviour
    {
        [SerializeField] private Transform player;

        [Header("Materials")]

        [SerializeField] private Material grayMaterial;

        private Dictionary<Vector2Int, Chunk> chunkDictionary;

        private Vector2Int lastPlayerCoords;

        [Header("Rendering Properties")]
        // DATA TO BE SENT TO UI SCRIPTS
        [SerializeField][Min(1)] private int renderDistance = 1; 

        private void Awake()
        {
            chunkDictionary = new Dictionary<Vector2Int, Chunk>();
            lastPlayerCoords = new Vector2Int(100, 100); // Arbitrary initial value.
        }

        private void LateUpdate()
        {
            Vector2Int playerCoords = ComputeChunkCoords(new Vector2(player.position.x, player.position.z), 100);

            if (lastPlayerCoords != playerCoords)
            {
                for (int x = -renderDistance; x <= renderDistance; x++)
                {
                    for (int y = -renderDistance; y <= renderDistance; y++)
                    {
                        Vector2Int currentCoord = new(playerCoords.x + x, playerCoords.y + y);

                        if (!chunkDictionary.ContainsKey(currentCoord))
                        {
                            Vector2 position = currentCoord * 100;
                            Chunk chunk = new(position, 1, 100, grayMaterial, null, null);
                            chunkDictionary.Add(currentCoord, chunk);
                        }
                    }
                }

                // HEAVILY UNPERFORMANT AT HIGH CHUNK COUNTS
                foreach (var chunk in chunkDictionary)
                {
                    Vector2Int delta = chunk.Key - playerCoords;
                    bool isWithinRange = Mathf.Abs(delta.x) <= renderDistance && Mathf.Abs(delta.y) <= renderDistance;
                    chunk.Value.gameObject.SetActive(isWithinRange);
                }

                lastPlayerCoords = playerCoords;
            }
        }

        private static Vector2Int ComputeChunkCoords(Vector2 positionXZ, int chunkSize) => new(Mathf.RoundToInt(positionXZ.x / chunkSize), Mathf.RoundToInt(positionXZ.y / chunkSize));

        [Button]
        public void GenerateTestChunk()
        {
            Chunk chunk = new(Vector2.zero, 1, 50, grayMaterial, null, null);
        }
    }
}
