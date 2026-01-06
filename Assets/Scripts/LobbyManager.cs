using System;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using static Assets.Scripts.Chunks;

namespace Assets.Scripts
{
    // +x and +z top view perspective.
    // None is the inital value of both Direction variables.
    public enum NextDirection
    {
        NONE, UP, DOWN, LEFT, RIGHT
    }

    public class LobbyManager : MonoBehaviour
    {
        [Header("Chunking Properties")]

        [SerializeField] [Min(1)] private int meshLength = 1;

        [SerializeField] [Min(1)] private int resolution = 1;

        [SerializeField] private Transform player;

        private GameObject chunkHolder;

        private static Dictionary<Vector3, SquareChunk> squareChunks;

        private static List<SquareChunk> chunksLastVisible;

        [Header("Maze Properties")]

        [SerializeField] private bool useRandomSeed = false;

        [SerializeField] private bool useTestVisuals = false;

        [Space(15f)]

        [SerializeField] private int seed;

        [SerializeField] [Range(1, 20)] private int segments = 2;

        [SerializeField] private float chanceThreshold = 1.0f;

        [SerializeField] private float minChance;

        [SerializeField] private float maxChance;

        [SerializeField] private float wallHeight = 1.0f;

        [SerializeField] private int minWallChain = 1;

        [SerializeField] private int maxWallChain = 3;

        [SerializeField] private int minWallLength = 3;

        [SerializeField] private int maxWallLength = 8;

        [Space(15f)]

        [SerializeField] private Material defaultMaterial;

        [SerializeField] private Material arrowWallpaper;

        [SerializeField] private Material carpet;

        private System.Random prng;

        private List<Point> startPoints;

        private readonly struct Directions
        {
            public static readonly Vector3 UP_v = new Vector3(0, 0, 1);

            public static readonly Vector3 DOWN_v = new Vector3(0, 0, -1);

            public static readonly Vector3 LEFT_v = new Vector3(-1, 0, 0);

            public static readonly Vector3 RIGHT_v = new Vector3(1, 0, 0);
        }

        private struct Point
        {
            public Vector3 nextPosition;

            public NextDirection nextDirection;

            public NextDirection ChooseRandomDirection(System.Random prng)
            {
                int rand = prng.Next(1, sizeof(NextDirection) + 1);

                switch (rand)
                {
                    case 1:
                        return NextDirection.UP;

                    case 2:
                        return NextDirection.DOWN;

                    case 3:
                        return NextDirection.LEFT;

                    case 4:
                        return NextDirection.RIGHT;
                }

                return NextDirection.NONE;
            }
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
            startPoints = new List<Point>();
            startPoints.Clear();

            SelectStartingPoints(chunk);
            CreateWalls(chunk);
        }

        private void SelectStartingPoints(SquareChunk chunk)
        {
            Vector3 bottomLeft = new Vector3(chunk.position.x - (chunk.length * 0.5f), 0, chunk.position.y - (chunk.length * 0.5f));

            float spacing = (float)chunk.length / segments;
            float chance = 0f;

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
                        point.nextPosition = new Vector3(posX, wallHeight * 0.5f, posZ);
                        startPoints.Add(point);

                        if (useTestVisuals)
                        {
                            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            cube.transform.position = point.nextPosition;
                            cube.transform.parent = chunk.gameObject.transform;
                        }

                        chance = 0f;
                    }
                }
            }
        }

        private void CreateWalls(SquareChunk chunk)
        {
            if (startPoints.Count <= 0)
            {
                throw new ArgumentException("There are no [startPoints] available! Method: [CreateWalls()]");
            }

            for (int i = 0; i < startPoints.Count; i++)
            {
                Point point = startPoints[i];

                GameObject chainParent = new GameObject($"Chain {i + 1}");
                chainParent.transform.parent = chunk.gameObject.transform;

                int iterations = prng.Next(minWallChain, maxWallChain);
                Vector3 previousDirection = Vector3.zero;

                for (int j = 0; j < iterations; j++)
                {
                    point.nextDirection = point.ChooseRandomDirection(prng);
                    Vector3 direction = SelectDirectionForNextWall(point, previousDirection);

                    float length = prng.Next(minWallLength, maxWallLength);

                    // Adding +1 simplifies the code and overlaps the walls, but due to the material it will not be noticeable.
                    float xAxisScale = point.nextDirection == NextDirection.LEFT || point.nextDirection == NextDirection.RIGHT ? length + 1 : 1;
                    float zAxisScale = point.nextDirection == NextDirection.UP || point.nextDirection == NextDirection.DOWN ? length + 1 : 1;

                    previousDirection = direction;

                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = point.nextPosition + direction * length * 0.5f;
                    wall.transform.localScale = new Vector3(xAxisScale, wallHeight, zAxisScale);
                    wall.GetComponent<MeshRenderer>().material = arrowWallpaper;

                    point.nextPosition += direction * length;

                    wall.transform.parent = chainParent.transform;

                    if (useTestVisuals)
                    {
                        GameObject otherRefPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        otherRefPoint.transform.position = point.nextPosition;
                        otherRefPoint.transform.parent = chainParent.transform;
                    }
                }
            }
        }

        private Vector3 SelectDirectionForNextWall(Point point, Vector3 previousDirection)
        {
            Vector3 direction = Vector3.zero;

            switch (point.nextDirection)
            {
                case NextDirection.UP:
                    direction = Directions.UP_v;
                    break;

                case NextDirection.DOWN:
                    direction = Directions.DOWN_v;
                    break;

                case NextDirection.LEFT:
                    direction = Directions.LEFT_v;
                    break;

                case NextDirection.RIGHT:
                    direction = Directions.RIGHT_v;
                    break;
            }

            return direction == -previousDirection ? -direction : direction;
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
                        chunk.gameObject.GetComponent<MeshRenderer>().material = carpet;
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
            float operationX = clientPosition.x / chunkLength;
            float operationY = clientPosition.z / chunkLength;

            if (clientPosition.x < 0)
            {
                return new Vector2(Mathf.CeilToInt(operationX), Mathf.CeilToInt(operationY));
            }

            return new Vector2(Mathf.FloorToInt(operationX), Mathf.FloorToInt(operationY));
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

        [Button]
        public void GenerateTestChunk()
        {
            int seedToUse = useRandomSeed ? UnityEngine.Random.Range(0, 10000000) : seed;

            prng = new System.Random(seedToUse);

            SquareChunk chunk = new SquareChunk(new Vector2(0, 0), meshLength, resolution, chunk => { GenerateLobbyMaze(chunk); });
            chunk.gameObject.GetComponent<MeshRenderer>().material = carpet;
        }
    }
}
