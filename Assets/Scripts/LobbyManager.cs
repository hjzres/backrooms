using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;
using static Assets.Scripts.Chunks;

namespace Assets.Scripts
{
    public class LobbyManager : MonoBehaviour
    {
        [Header("Chunking Properties")]

        [SerializeField] [Min(1)] private int meshLength = 1;

        [SerializeField] [Min(1)] private int resolution = 1;

        [SerializeField] private Material defaultMaterial;

        [SerializeField] private Transform player;

        private static Dictionary<Vector3, SquareChunk> squareChunks;

        private static List<SquareChunk> chunksLastVisible;

        [Header("Maze Properties")]

        [SerializeField] private int segments = 2;

        private void Awake()
        {
            squareChunks = new Dictionary<Vector3, SquareChunk>();
            chunksLastVisible = new List<SquareChunk>();
        }

        private void LateUpdate()
        {
            UpdateClientChunks();
        }

        private void GenerateLobbyMaze(SquareChunk chunk)
        {
            
        }

        private void UpdateClientChunks()
        {
            Vector2 clientChunkCoord = ComputeCliendChunkCoords(player.position, meshLength);

            for (int x = -clientDistanceInChunks; x <= clientDistanceInChunks; x++)
            {
                for (int y = -clientDistanceInChunks; y <= clientDistanceInChunks; y++)
                {
                    Vector2 coordinates = new Vector2(clientChunkCoord.x + x, clientChunkCoord.y + y);

                    if (!squareChunks.ContainsKey(new Vector3(coordinates.x, 0, coordinates.y)))
                    {
                        Vector2 chunkPosition = coordinates * meshLength;
                        SquareChunk chunk = new SquareChunk(chunkPosition, meshLength, 1, chunk => { GenerateLobbyMaze(chunk); });
                        chunk.gameObject.GetComponent<MeshRenderer>().material = defaultMaterial;

                        squareChunks.Add(new Vector3(coordinates.x, 0, coordinates.y), chunk);
                    }

                    else
                    {

                    }
                }
            }
        }

        private Vector2 ComputeCliendChunkCoords(Vector3 clientPosition, int chunkLength)
        {
            float operationX = clientPosition.x / (chunkLength);
            float operationY = clientPosition.z / (chunkLength);

            if (clientPosition.x < 0)
            {
                return new Vector2(Mathf.CeilToInt(operationX), Mathf.CeilToInt(operationY));
            }

            return new Vector2(Mathf.FloorToInt(operationX), Mathf.FloorToInt(operationY));
        }

        [Button]
        public void GenerateTestChunk()
        {
            SquareChunk chunk = new SquareChunk(new Vector2(0, 0), meshLength, resolution, chunk => { GenerateLobbyMaze(chunk); });
            chunk.gameObject.GetComponent<MeshRenderer>().material = defaultMaterial;
        }
    }
}
