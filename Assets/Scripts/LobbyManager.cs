using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Assets.Scripts.Chunks;
using static UnityEngine.Rendering.PostProcessing.HistogramMonitor;

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

            public readonly NextDirection RandomizeDirection(System.Random prng)
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

        private struct LobbyWall
        {
            public GameObject gameObject;

            private readonly Action<LobbyWall> onCreate;

            public LobbyWall(Vector3 position, Vector3 scale, Material material, Action<LobbyWall> onCreate)
            {
                gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gameObject.transform.position = position;
                gameObject.transform.localScale = scale;
                gameObject.GetComponent<MeshRenderer>().material = material;

                this.onCreate = onCreate;
                onCreate?.Invoke(this);
            }
        }

        [System.Serializable]
        public struct Decoration
        {
            public GameObject prefab;

            public int spawnChance; // Interpret as 1 / spawnChance.

            public float positionOffsetY;

            public float offsetReductionXZ;
        }

        // ------------------------------------------------------------------------------------------- //

        [SerializeField] private Transform player;

        [Header("Chunking Properties")]

        [SerializeField] [Min(1)] private int meshLength = 1;

        [SerializeField] [Min(1)] private int chunkResolution = 1;

        [SerializeField] private int viewDistanceInChunks = 1;

        [Space(15f)]

        [SerializeField] private int mazeSpawnChance = 85;

        [SerializeField] private int repetitiveWallsSpawnChance = 10;

        [SerializeField] private int pitfallsSpawnChance = 5;

        //[SerializeField] private Vector3 noiseThresholds = Vector3.zero;

        [Header("Maze Properties")]

        [SerializeField] private bool useRandomSeed = false;

        [SerializeField] private bool useTestVisuals = false;

        [Space(15f)]

        [SerializeField] private int seed; // MULTIPLAYER ONLY NEEDS THIS SENT TO CLIENTS.

        [SerializeField] [Range(1, 20)] private int segments = 2;

        [SerializeField] private float chanceThreshold = 1.0f;

        [SerializeField] private float minChance;

        [SerializeField] private float maxChance;

        [SerializeField] private float wallHeight = 1.0f;

        [SerializeField] private int minWallChain = 1;

        [SerializeField] private int maxWallChain = 3;

        [SerializeField] private int minWallLength = 3;

        [SerializeField] private int maxWallLength = 8;

        [Header("Materials")]

        [SerializeField] private Material defaultMaterial;

        [SerializeField] private Material arrowWallpaper;

        [SerializeField] private Material carpet;

        [Header("Wall Decorations")]
        [SerializeField] private Decoration wallOutlet; // 13, -2, 0.

        // ------------------------------------------------------------------------------------------- //

        private GameObject generatedChunksContainer;

        private static Dictionary<Vector2Int, SquareChunk> squareChunks;

        private System.Random prng; // MUST BE CREATED ONCE.

        private List<Point> startPoints;

        private List<Decoration> decorations;

        // ------------------------------------------------------------------------------------------- //

        private void Awake()
        {
            squareChunks = new Dictionary<Vector2Int, SquareChunk>();

            generatedChunksContainer = new GameObject("Generated Chunks");
            prng = new System.Random(seed);

            AddDecorationsToList();
        }

        private void LateUpdate()
        {
            UpdateClientChunks();
        }

        // Unfortunately has to be hard coded.
        private void AddDecorationsToList()
        {
            decorations = new List<Decoration>()
            {
                wallOutlet
            };
        }

        private void GenerateLobbyLevel(SquareChunk chunk, Vector2 coordinates)
        {
            float noise = Noise.WhiteNoise(new Vector2(coordinates.x + seed, coordinates.y + seed)) * 100;

            if (noise < mazeSpawnChance)
            {
                GenerateMaze(chunk);
            }

            else if (noise >= mazeSpawnChance && noise < repetitiveWallsSpawnChance + mazeSpawnChance)
            {
                GenerateRepetitiveWalls(chunk);
            }
        }

        private void GenerateMaze(SquareChunk chunk)
        {
            startPoints = new List<Point>();
            startPoints.Clear();

            RandomizeStartingPoints(chunk);
            CreateWalls(chunk);
        }

        private void RandomizeStartingPoints(SquareChunk chunk)
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
                throw new ArgumentException("There are no { startPoints } available! [Method: CreateWalls()].");
            }

            for (int i = 0; i < startPoints.Count; i++)
            {
                int iterations = prng.Next(minWallChain, maxWallChain);

                Point point = startPoints[i];
                Vector3 previousDirection = Vector3.zero;

                GameObject chainParent = new GameObject($"Chain {i + 1}");
                chainParent.transform.parent = chunk.gameObject.transform;

                for (int j = 0; j < iterations; j++)
                {
                    point.nextDirection = point.RandomizeDirection(prng);
                    Vector3 direction = GetDirectionForNextWall(point, previousDirection);

                    int length = prng.Next(minWallLength, maxWallLength);

                    // Adding +1 simplifies the code and overlaps the walls, but due to the material it will not be noticeable.
                    int xAxisScale = point.nextDirection == NextDirection.LEFT || point.nextDirection == NextDirection.RIGHT ? length + 1 : 1;
                    int zAxisScale = point.nextDirection == NextDirection.UP || point.nextDirection == NextDirection.DOWN ? length + 1 : 1;

                    previousDirection = direction;

                    Vector3 wallPosition = point.nextPosition + (0.5f * length * direction);
                    Vector3 wallScale = new Vector3(xAxisScale, wallHeight, zAxisScale);
                    int decorationIndex = prng.Next(0, decorations.Count); // Have a chance to spawn one of the decorations from the list.
                    int scaleInRespectToDirection = Mathf.Max(xAxisScale, zAxisScale);

                    LobbyWall wall = new LobbyWall(wallPosition, wallScale, arrowWallpaper, 
                        onCreate => AddDecorationsOnWall(decorationIndex, scaleInRespectToDirection, wallPosition, previousDirection, chainParent.transform));

                    point.nextPosition += direction * length;
                    wall.gameObject.transform.parent = chainParent.transform;
                }
            }

            startPoints.Clear();
        }

        private Vector3 GetDirectionForNextWall(Point point, Vector3 previousDirection)
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

        private void AddDecorationsOnWall(int decorationIndex, float wallScaleFactor, Vector3 wallPosition, Vector3 wallDirection, Transform parent, bool addBaseboards = false)
        {
            bool canSpawn = prng.Next(1, wallOutlet.spawnChance) == 1;

            if (!canSpawn) 
            { 
                return; 
            }

            Decoration deco = decorations[decorationIndex];
            wallScaleFactor *= 0.4f; // Decrease random offset area.

            GameObject decoObject = Instantiate(deco.prefab, parent);

            int randomSide = prng.Next(1, 10) < 5 ? -1 : 1;

            float offsetX = wallDirection == Directions.UP_v || wallDirection == Directions.DOWN_v ? 0.5f * randomSide: 0;
            float offsetZ = wallDirection == Directions.LEFT_v || wallDirection == Directions.RIGHT_v ? -0.5f * randomSide: 0;

            // All prefabs must be offset in the +x direction, with the main face looking in that direction and the origin at (0,0,0).
            float rot = offsetZ == 0 ? (randomSide == -1 ? 180 : 0) : (randomSide == -1 ? 270 : 90);

            float min = -wallScaleFactor + deco.offsetReductionXZ;
            float max = wallScaleFactor - deco.offsetReductionXZ;

            float randOffsetX = offsetX == 0 ? NextFloat(min, max) : 0;
            float randOffsetZ = offsetZ == 0 ? NextFloat(min, max) : 0;

            Vector3 offsets = new Vector3(offsetX + randOffsetX, wallOutlet.positionOffsetY, offsetZ + randOffsetZ);

            decoObject.transform.SetPositionAndRotation(wallPosition + offsets, Quaternion.Euler(deco.prefab.transform.rotation.x, deco.prefab.transform.rotation.y + rot, deco.prefab.transform.rotation.z));
        }

        private void GenerateRepetitiveWalls(SquareChunk chunk)
        {
            Vector3 bottomLeft = new Vector3(chunk.position.x - (chunk.length * 0.5f), 0, chunk.position.y - (chunk.length * 0.5f));

            int reducedSegments = Mathf.FloorToInt(segments * 0.8f);
            float spacing = (float)chunk.length / reducedSegments;

            for (int x = 0; x <= reducedSegments; x++)
            {
                for (int y = 0; y <= reducedSegments; y++)
                {
                    /*if ((x == 0 || x == reducedSegments || y == 0 || y == reducedSegments))
                    {
                        continue;
                    }*/

                    float posX = bottomLeft.x + (x * spacing);
                    float posZ = bottomLeft.z + (y * spacing);

                    Vector3 position = new Vector3(posX, wallHeight * 0.5f, posZ);
                    Vector3 scale = new Vector3(1.5f, wallHeight, 1.5f);

                    LobbyWall wall = new LobbyWall(position, scale, arrowWallpaper, null);
                    wall.gameObject.transform.parent = chunk.gameObject.transform;
                }
            }
        }

        private void GeneratePitfalls()
        {

        }

        private void UpdateClientChunks()
        {
            Vector2Int clientChunkCoord = ComputeCliendChunkCoords(player.position, meshLength);

            for (int x = -viewDistanceInChunks; x <= viewDistanceInChunks; x++)
            {
                for (int y = -viewDistanceInChunks; y <= viewDistanceInChunks; y++)
                {
                    Vector2Int coordinates = new Vector2Int(clientChunkCoord.x + x, clientChunkCoord.y + y);

                    if (!squareChunks.ContainsKey(coordinates))
                    {
                        Vector2 chunkPosition = coordinates * meshLength;
                        SquareChunk chunk = new SquareChunk(chunkPosition, meshLength, 1, chunk => { GenerateLobbyLevel(chunk, coordinates); });
                        chunk.gameObject.GetComponent<MeshRenderer>().material = carpet;
                        chunk.gameObject.transform.parent = generatedChunksContainer.transform;

                        squareChunks.Add(coordinates, chunk);
                    }
                }
            }

            // AI assisted, I could not figure out how to disable them for the life of me.
            // Will definitely come back and rewrite this, as a foreach loop is kind of unoptimal
            // when I feel that it could be done in the nested loops instead.
            foreach (var chunk in squareChunks) 
            { 
                Vector2Int delta = chunk.Key - clientChunkCoord; 
                bool isWithinRange = Mathf.Abs(delta.x) <= viewDistanceInChunks && Mathf.Abs(delta.y) <= viewDistanceInChunks; 
                chunk.Value.gameObject.SetActive(isWithinRange); 
            }
        }

        private Vector2Int ComputeCliendChunkCoords(Vector3 clientPosition, int chunkLength)
        {
            int operationX = Mathf.RoundToInt(clientPosition.x / chunkLength);
            int operationY = Mathf.RoundToInt(clientPosition.z / chunkLength);

            return new Vector2Int(operationX, operationY);
        }

        private float NextFloat(float min, float max)
        {
            if (min > max)
            {
                throw new ArgumentException("Variable 'min' must be smaller than variable 'max' [Method: NextFloat()].");
            }

            double range = max - min;
            return (float)(min + prng.NextDouble() * range);
        }

        [Button]
        public void GenerateTestChunk()
        {
            int seedToUse = useRandomSeed ? UnityEngine.Random.Range(0, 10000000) : seed;
            AddDecorationsToList();

            prng = new System.Random(seedToUse);

            SquareChunk chunk = new SquareChunk(new Vector2(0, 0), meshLength, chunkResolution, chunk => { GenerateLobbyLevel(chunk, Vector2.zero); });
            chunk.gameObject.GetComponent<MeshRenderer>().material = carpet;
        }
    }
}
