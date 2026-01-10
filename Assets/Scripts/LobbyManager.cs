using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEngine;
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

            public Transform transform;

            private readonly Action<LobbyWall> onCreate;

            public LobbyWall(Vector3 position, Vector3 scale, Material material, Action<LobbyWall> onCreate)
            {
                gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                transform = gameObject.transform;
                transform.position = position;
                transform.localScale = scale;

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

            public bool useOffsets;
        }

        // ------------------------------------------------------------------------------------------- //

        [SerializeField] private Transform player;

        [Header("Chunking Properties")]

        [SerializeField] [Min(1)] private int meshLength = 1;

        [SerializeField] private int viewDistanceInChunks = 1;

        [Space(15f)]

        [SerializeField] private int mazeSpawnChance = 85;

        [SerializeField] private int repetitiveWallsSpawnChance = 10;

        [SerializeField] private int pitfallsSpawnChance = 5;

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

        [Header("Repetitive Walls Properties")]

        [SerializeField] private int repetitiveWallSegments = 10;

        [SerializeField] private float repetitiveWallsThickness = 1.5f;

        [Header("Pitfalls Properties")]

        [SerializeField] private int pitfallNumber = 5;

        [SerializeField] private float pitfallThickness = 1;

        [SerializeField] private int pitfallDepth = 10;

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
            float noise = Noise.WhiteNoise2D(new Vector2(coordinates.x + seed, coordinates.y + seed)) * 100;

            if (noise < mazeSpawnChance)
            {
                GenerateMaze(chunk);
            }

            else if (noise >= mazeSpawnChance && noise < mazeSpawnChance + repetitiveWallsSpawnChance)
            {
                GenerateRepetitiveWalls(chunk);
            }

            else if (noise >= repetitiveWallsSpawnChance && noise <= mazeSpawnChance + repetitiveWallsSpawnChance + pitfallsSpawnChance)
            {
                GeneratePitfalls(chunk);
            }

            GameObject ceilingContainer = new GameObject("Ceiling");
            SquareChunk ceiling = new SquareChunk(coordinates, meshLength, 1, defaultMaterial, null);

            ceiling.transform.parent = ceilingContainer.transform;
            ceiling.gameObject.isStatic = true;

            ceilingContainer.transform.parent = chunk.transform;
            ceilingContainer.transform.SetPositionAndRotation(new Vector3(ceilingContainer.transform.position.x, wallHeight, ceilingContainer.transform.position.z), Quaternion.Euler(180, 0, 0));
            ceilingContainer.isStatic = true;
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
            Vector2 bottomLeft = new Vector2(chunk.position.x - chunk.length * 0.5f, chunk.position.y - chunk.length * 0.5f);

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
                        float posZ = bottomLeft.y + (y * spacing);

                        Point point = new Point 
                        { 
                            nextPosition = new Vector3(posX, wallHeight * 0.5f, posZ) 
                        };

                        startPoints.Add(point);
                        chance = 0f;

                        if (useTestVisuals)
                        {
                            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            sphere.transform.position = point.nextPosition;
                            sphere.transform.parent = chunk.transform;
                            sphere.isStatic = true;
                        }
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
                chainParent.transform.parent = chunk.transform;

                for (int j = 0; j < iterations; j++)
                {
                    point.nextDirection = point.RandomizeDirection(prng);
                    Vector3 direction = GetDirectionForNextWall(point, previousDirection);

                    int length = prng.Next(minWallLength, maxWallLength);

                    // Adding +1 simplifies the code and overlaps the walls, but due to the material it will not be noticeable.
                    int xAxisScale = point.nextDirection == NextDirection.LEFT || point.nextDirection == NextDirection.RIGHT ? length + 1 : 1;
                    int zAxisScale = point.nextDirection == NextDirection.UP || point.nextDirection == NextDirection.DOWN ? length + 1 : 1;

                    // Have a chance to spawn one of the decorations from the list.
                    int decorationIndex = prng.Next(0, decorations.Count); 
                    int scaleInRespectToDirection = Mathf.Max(xAxisScale, zAxisScale);

                    previousDirection = direction;

                    Vector3 position = point.nextPosition + 0.5f * length * direction;
                    Vector3 scale = new Vector3(xAxisScale, wallHeight, zAxisScale);

                    LobbyWall wall = new LobbyWall(
                        position, 
                        scale, 
                        arrowWallpaper, 
                        onCreate => AddDecorationsOnWall(decorationIndex, scaleInRespectToDirection, position, previousDirection, chainParent.transform)
                    );

                    point.nextPosition += direction * length;
                    wall.transform.parent = chainParent.transform;
                    wall.gameObject.isStatic = true;
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

            float randOffsetX = offsetX == 0 && deco.useOffsets ? NextFloat(min, max) : 0;
            float randOffsetZ = offsetZ == 0 && deco.useOffsets ? NextFloat(min, max) : 0;

            float offsetY = deco.useOffsets ? wallOutlet.positionOffsetY : 0;

            Vector3 offsets = new Vector3(offsetX + randOffsetX, offsetY, offsetZ + randOffsetZ);
            Quaternion currentRotation = decoObject.transform.rotation;

            decoObject.transform.SetPositionAndRotation(wallPosition + offsets, Quaternion.Euler(currentRotation.x, currentRotation.y + rot, currentRotation.z));
            decoObject.isStatic = true;
        }

        private void GenerateRepetitiveWalls(SquareChunk chunk)
        {
            Vector2 bottomLeft = new Vector2(chunk.position.x - chunk.length * 0.5f, chunk.position.y - chunk.length * 0.5f);

            float spacing = (float)chunk.length / repetitiveWallSegments;

            for (int x = 0; x <= repetitiveWallSegments; x++)
            {
                for (int y = 0; y <= repetitiveWallSegments; y++)
                {
                    Vector3 position = new Vector3(bottomLeft.x + x * spacing, wallHeight * 0.5f, bottomLeft.y + y * spacing);
                    Vector3 scale = new Vector3(repetitiveWallsThickness, wallHeight, repetitiveWallsThickness);

                    LobbyWall wall = new LobbyWall(position, scale, arrowWallpaper, null);
                    wall.transform.parent = chunk.transform;
                    wall.gameObject.isStatic = true;
                }
            }
        }

        private void GeneratePitfalls(SquareChunk chunk)
        {
            Vector2 bottomLeft = new Vector2(chunk.position.x - chunk.length * 0.5f, chunk.position.y - chunk.length * 0.5f);

            float spacing = (float)chunk.length / pitfallNumber;
            chunk.transform.position = new Vector3(chunk.transform.position.x, -pitfallDepth, chunk.transform.position.z);

            for (int x = 0; x <= pitfallNumber; x++)
            {
                for (int y = 0; y <= pitfallNumber; y++)
                {
                    float posOffset = spacing * 0.5f;
                    float posX = bottomLeft.x + x * spacing;
                    float posZ = bottomLeft.y + y * spacing;

                    if (x < pitfallNumber)
                    {
                        LobbyWall wallX = new LobbyWall(
                            new Vector3(posX + posOffset, pitfallDepth * -0.5f, posZ), 
                            new Vector3(spacing, pitfallDepth, pitfallThickness), 
                            carpet, 
                            null
                        );

                        wallX.transform.parent = chunk.transform;
                        wallX.gameObject.isStatic = true;
                    }

                    if (y < pitfallNumber)
                    {
                        LobbyWall wallZ = new LobbyWall(
                            new Vector3(posX, pitfallDepth * -0.5f, posZ + posOffset), 
                            new Vector3(pitfallThickness, pitfallDepth, spacing), 
                            carpet, 
                            null
                        );

                        wallZ.transform.parent = chunk.transform;
                        wallZ.gameObject.isStatic = true;
                    }
                }
            }
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
                        SquareChunk chunk = new SquareChunk(chunkPosition, meshLength, 1, defaultMaterial, chunk => { GenerateLobbyLevel(chunk, chunkPosition); });
                        chunk.transform.parent = generatedChunksContainer.transform;
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

            SquareChunk chunk = new SquareChunk(Vector2.zero, meshLength, 1, defaultMaterial, chunk => { GenerateLobbyLevel(chunk, Vector2.zero); });
            chunk.gameObject.GetComponent<MeshRenderer>().material = carpet;

            GameObject ceilingContainer = new GameObject("Ceiling");
            SquareChunk ceiling = new SquareChunk(Vector2.zero, meshLength, 1, defaultMaterial, null);

            ceiling.gameObject.transform.parent = ceilingContainer.transform;
            ceilingContainer.transform.parent = chunk.transform;
            ceilingContainer.transform.position = new Vector3(ceilingContainer.transform.position.x, wallHeight, ceilingContainer.transform.position.z);
            ceilingContainer.transform.rotation = Quaternion.Euler(180, 0, 0);
        }
    }
}
