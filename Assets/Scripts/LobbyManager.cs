using System;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
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

        private GameObject chunkHolder;

        private static Dictionary<Vector3, SquareChunk> squareChunks;

        private static List<SquareChunk> chunksLastVisible;

        [Header("Maze Properties")]

        [SerializeField] private bool useRandomSeed = false;

        [SerializeField] private bool useTestVisuals = false;

        [SerializeField] private int seed;

        [SerializeField] [Range(1, 20)] private int segments = 2;

        [SerializeField] private float chanceThreshold = 1.0f;

        [SerializeField] private float minChance;

        [SerializeField] private float maxChance;

        private System.Random prng;

        private List<Point> startPoints;

        private struct Point
        {
            public Vector3 initialPosition;

            public Vector3 length;
        }

        private void Awake()
        {
            squareChunks = new Dictionary<Vector3, SquareChunk>();
            chunksLastVisible = new List<SquareChunk>();

            chunkHolder = new GameObject("Active Chunks");
            prng = new System.Random(seed);
        }

        private void LateUpdate()
        {
            UpdateClientChunks();
        }

        private void GenerateLobbyMaze(SquareChunk chunk)
        {
            SelectStartingPoints(chunk);
            CreateWalls(chunk);
        }

        private void SelectStartingPoints(SquareChunk chunk)
        {
            Vector3 bottomLeft = new Vector3(chunk.position.x - (chunk.length * 0.5f), 0, chunk.position.y - (chunk.length * 0.5f));

            float spacing = (float)chunk.length / segments;
            float chance = 0f;

            startPoints = new List<Point>();
            startPoints.Clear();

            for (int x = 0; x <= segments; x++)
            {
                for (int y = 0; y <= segments; y++)
                {
                    if ((x == 0 || x == segments || y == 0 || y == segments) && chance > minChance)
                    {
                        chance -= minChance;
                        continue;
                    }

                    chance += NextFloat(minChance, maxChance);

                    if (chance >= chanceThreshold)
                    {
                        float posX = bottomLeft.x + (x * spacing);
                        float posZ = bottomLeft.z + (y * spacing);

                        Point point = new Point();
                        point.initialPosition = new Vector3(posX, 0, posZ);
                        startPoints.Add(point);

                        if (useTestVisuals)
                        {
                            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            cube.transform.position = point.initialPosition;
                            cube.transform.parent = chunk.gameObject.transform;
                        }

                        chance = 0f;
                    }
                }
            }
        }

        private float NextFloat(float min, float max)
        {
            if (min > max)
            {
                throw new ArgumentException("Variable 'min' must be smaller than variable 'max' [Src: NextFloat method].");
            }

            double range = max - min;
            return (float)(min + prng.NextDouble() * range);
        }

        private void CreateWalls(SquareChunk chunk)
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
                        chunk.gameObject.transform.parent = chunkHolder.transform;

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
            int seedToUse = useRandomSeed ? UnityEngine.Random.Range(0, 10000000) : seed;

            prng = new System.Random(seedToUse);

            SquareChunk chunk = new SquareChunk(new Vector2(0, 0), meshLength, resolution, chunk => { GenerateLobbyMaze(chunk); });
            chunk.gameObject.GetComponent<MeshRenderer>().material = defaultMaterial;
        }
    }
}
