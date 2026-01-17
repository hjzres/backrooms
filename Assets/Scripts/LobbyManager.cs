using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.UIElements;
using static Assets.Scripts.Chunks;
using static UnityEditor.PlayerSettings;

namespace Assets.Scripts
{
    // +x and +z top view perspective.
    // None is the inital value of both Direction variables.
    public enum NextDirection
    {
        NONE, UP, DOWN, LEFT, RIGHT
    }

    public enum ChunkID
    {
        MAZE, REPETITIVE, PITFALL
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
                gameObject.isStatic = true;

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

            public bool useRandomOffsets;
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

        [SerializeField] private float wallHeight = 1.0f;

        [SerializeField] private Vector2 pointChance = new Vector2(0.1f, 1);

        [SerializeField] private Vector2Int wallChainRange = new Vector2Int(3, 7);
        
        [SerializeField] private Vector2Int wallLengthRange = new Vector2Int(3, 8);

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

        [SerializeField] private LayerMask decorationCastMask;

        [SerializeField] private float decorationSphereCastRadius = 1.0f;

        [SerializeField] private float decorationSphereCastDistance = 10f;

        [Header("Light Properties")]

        [SerializeField] private LayerMask lightCastMask;

        [SerializeField] private int lightNumber;

        [SerializeField] private float lightSphereCastRadius = 1f;

        [SerializeField] private float lightSphereCastDistance = 10f;

        public GameObject lightPrefab;

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

            decorations = new List<Decoration>()
            {
                wallOutlet
            };
        }

        private void LateUpdate()
        {
            UpdateClientChunks();
        }

        private void GenerateLobbyLevel(SquareChunk chunk, Vector2 coordinates)
        {
            float noise = Noise.WhiteNoise2D(new Vector2(coordinates.x + seed, coordinates.y + seed)) * 100;
            Vector2 bottomLeft = new Vector2(chunk.position.x - chunk.length * 0.5f, chunk.position.y - chunk.length * 0.5f);

            if (noise < mazeSpawnChance)
            {
                chunk.ID = (int)ChunkID.MAZE;   
                GenerateMaze(chunk, bottomLeft);
            }

            else if (noise >= mazeSpawnChance && noise < mazeSpawnChance + repetitiveWallsSpawnChance)
            {
                chunk.ID = (int)ChunkID.REPETITIVE;
                GenerateRepetitiveWalls(chunk, bottomLeft);
            }

            else if (noise >= repetitiveWallsSpawnChance && noise <= mazeSpawnChance + repetitiveWallsSpawnChance + pitfallsSpawnChance)
            {
                chunk.ID = (int)ChunkID.PITFALL;
                GeneratePitfalls(chunk, bottomLeft);
            }

            SquareChunk ceiling = new SquareChunk(coordinates, meshLength, 1, defaultMaterial, null);
            ceiling.gameObject.name = "Ceiling";
            ceiling.transform.SetPositionAndRotation(new Vector3(ceiling.transform.position.x, wallHeight, ceiling.transform.position.z + meshLength), Quaternion.Euler(new Vector3(180, 0, 0)));
            ceiling.transform.parent = chunk.transform;
            ceiling.gameObject.isStatic = true;

            AddLightsToCeiling(chunk, bottomLeft);

            // Check Chunk ID THEN run AddLightsOnCeiling (exclude that of pitfalls for aura).
            // Make a chance for no lights to spawn based on noise?
            if (chunk.ID != (int)ChunkID.PITFALL)
            {
                
                //StartCoroutine(CheckCeilingLightCollisions());
            }
        }

        private void GenerateMaze(SquareChunk chunk, Vector2 bottomLeft)
        {
            startPoints = new List<Point>();
            startPoints.Clear();

            RandomizeStartingPoints(chunk, bottomLeft);
            CreateWalls(chunk);
        }

        private void RandomizeStartingPoints(SquareChunk chunk, Vector2 bottomLeft)
        {
            float spacing = (float)chunk.length / segments;
            float chance = 0f;

            for (int x = 0; x <= segments; x++)
            {
                for (int y = 0; y <= segments; y++)
                {
                    if ((x == 0 || x == segments || y == 0 || y == segments) && chance > pointChance.x)
                    {
                        chance -= pointChance.x;
                        continue;
                    }

                    chance += NextFloat(pointChance.x, pointChance.y);

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
                int iterations = prng.Next(wallChainRange.x, wallChainRange.y);

                Point point = startPoints[i];
                Vector3 previousDirection = Vector3.zero;

                GameObject chainParent = new GameObject($"Chain {i + 1}");
                chainParent.transform.parent = chunk.transform;

                for (int j = 0; j < iterations; j++)
                {
                    point.nextDirection = point.RandomizeDirection(prng);
                    Vector3 direction = GetDirectionForNextWall(point, previousDirection);

                    int length = prng.Next(wallLengthRange.x, wallLengthRange.y);

                    // Adding +1 simplifies the code and overlaps the walls, but due to the material it will not be noticeable.
                    int xAxisScale = point.nextDirection == NextDirection.LEFT || point.nextDirection == NextDirection.RIGHT ? length + 1 : 1;
                    int zAxisScale = point.nextDirection == NextDirection.UP || point.nextDirection == NextDirection.DOWN ? length + 1 : 1;

                    // Have a chance to spawn one of the decorations from the list.
                    int decorationIndex = prng.Next(0, decorations.Count); 
                    int scaleInRespectToDirection = Mathf.Max(xAxisScale, zAxisScale);

                    previousDirection = direction;

                    Vector3 position = point.nextPosition + 0.5f * length * direction;
                    Vector3 scale = new Vector3(xAxisScale, wallHeight, zAxisScale);

                    LobbyWall wall = new LobbyWall(position, scale, arrowWallpaper, onCreate => AddDecorationsOnWall(decorationIndex, scaleInRespectToDirection, position, previousDirection, chainParent.transform));
                    wall.transform.parent = chainParent.transform;
                    wall.gameObject.layer = 4; // TEMPORARY
                    point.nextPosition += direction * length;
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

            int randomSide = prng.Next(1, 10) < 5 ? -1 : 1;

            float offsetX = wallDirection == Directions.UP_v || wallDirection == Directions.DOWN_v ? 0.5f * randomSide : 0;
            float offsetZ = wallDirection == Directions.LEFT_v || wallDirection == Directions.RIGHT_v ? -0.5f * randomSide : 0;

            wallScaleFactor *= 0.4f; // Decrease random offset area.
            float randOffsetX = offsetX == 0 && deco.useRandomOffsets ? NextFloat(-wallScaleFactor + deco.offsetReductionXZ, wallScaleFactor - deco.offsetReductionXZ) : 0;
            float randOffsetZ = offsetZ == 0 && deco.useRandomOffsets ? NextFloat(-wallScaleFactor + deco.offsetReductionXZ, wallScaleFactor - deco.offsetReductionXZ) : 0;

            Vector3 offsets = new Vector3(offsetX + randOffsetX, deco.positionOffsetY, offsetZ + randOffsetZ);
            Vector3 sphereCastPosition = new Vector3(wallPosition.x + offsets.x * (decorationSphereCastRadius + 1), wallPosition.y + offsets.y, wallPosition.z + offsets.z * (decorationSphereCastRadius + 1));

            // All prefabs must be offset in the +x direction, with the main face looking in that direction and the origin at (0,0,0).
            float rotY = offsetZ == 0 ? (randomSide == -1 ? 180 : 0) : (randomSide == -1 ? 270 : 90);
            Vector3 localForward = rotY == 0 ? Directions.RIGHT_v : (rotY == 180 ? Directions.LEFT_v : (rotY == 270 ? Directions.DOWN_v : Directions.UP_v));

            StartCoroutine(SphereCastCollisionCheck(0.1f, sphereCastPosition, decorationSphereCastRadius, localForward, decorationSphereCastDistance, decorationCastMask, () => CreatePrefab(lightPrefab, wallPosition + offsets, new Vector3(0, rotY, 0), parent)));
        }

        private void GenerateRepetitiveWalls(SquareChunk chunk, Vector2 bottomLeft)
        {
            float spacing = (float)chunk.length / repetitiveWallSegments;

            for (int x = 0; x <= repetitiveWallSegments; x++)
            {
                for (int y = 0; y <= repetitiveWallSegments; y++)
                {
                    Vector3 position = new Vector3(bottomLeft.x + x * spacing, wallHeight * 0.5f, bottomLeft.y + y * spacing);
                    Vector3 scale = new Vector3(repetitiveWallsThickness, wallHeight, repetitiveWallsThickness);

                    LobbyWall wall = new LobbyWall(position, scale, arrowWallpaper, null);
                    wall.transform.parent = chunk.transform;
                }
            }
        }

        private void GeneratePitfalls(SquareChunk chunk, Vector2 bottomLeft)
        {
            float spacing = (float)chunk.length / pitfallNumber;
            chunk.transform.position = new Vector3(chunk.transform.position.x, -pitfallDepth, chunk.transform.position.z);

            for (int x = 0; x <= pitfallNumber; x++)
            {
                for (int y = 0; y <= pitfallNumber; y++)
                {
                    float posX = bottomLeft.x + x * spacing;
                    float posZ = bottomLeft.y + y * spacing;
                    float offset = spacing * 0.5f;

                    if (x < pitfallNumber)
                    {
                        LobbyWall wallX = new LobbyWall(new Vector3(posX + offset, pitfallDepth * -0.5f, posZ), new Vector3(spacing, pitfallDepth, pitfallThickness), carpet, null);
                        wallX.transform.parent = chunk.transform;
                    }

                    if (y < pitfallNumber)
                    {
                        LobbyWall wallZ = new LobbyWall(new Vector3(posX, pitfallDepth * -0.5f, posZ + offset), new Vector3(pitfallThickness, pitfallDepth, spacing), carpet, null);
                        wallZ.transform.parent = chunk.transform;
                    }
                }
            }
        }

        private void AddLightsToCeiling(SquareChunk chunk, Vector2 bottomLeft)
        {
            float spacing = (float)chunk.length / lightNumber;

            GameObject lightContainer = new GameObject("Lights");
            lightContainer.transform.parent = chunk.transform;

            for (int x = 0; x < lightNumber; x++)
            {
                for (int y = 0; y < lightNumber; y++)
                {
                    float posX = bottomLeft.x + x * spacing;
                    float posZ = bottomLeft.y + y * spacing;

                    Vector3 position = new Vector3(posX, wallHeight, posZ);
                    Vector3 castPosition = position + new Vector3(0, 5, 0);

                    StartCoroutine(SphereCastCollisionCheck(0.1f, castPosition, lightSphereCastRadius, Vector3.down.normalized, lightSphereCastDistance, lightCastMask, () => CreatePrefab(lightPrefab, position, Vector3.zero, lightContainer.transform)));
                }
            }
        }

        private void CreatePrefab(GameObject prefab, Vector3 position, Vector3 eulerAngles, Transform parent)
        {
            if (prefab == null)
            {
                throw new ArgumentException("Prefab isn't assigned in the inspector! [Method: CreatePrefab()]");
            }

            GameObject instance = Instantiate(prefab);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(eulerAngles));
            instance.transform.parent = parent;
            instance.isStatic = true;
        }

        private IEnumerator SphereCastCollisionCheck(float delayTime, Vector3 castPosition, float radius, Vector3 castDirection, float maxDistance, LayerMask mask, Action logic)
        {
            yield return new WaitForSecondsRealtime(delayTime);

            RaycastHit hit = new RaycastHit();
            bool castHit = Physics.SphereCast(castPosition, radius, castDirection, out hit, maxDistance, mask);

            if (!castHit)
            {
                logic?.Invoke();
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
                        SquareChunk chunk = new SquareChunk(chunkPosition, meshLength, 1, carpet, chunk => { GenerateLobbyLevel(chunk, chunkPosition); });
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
            decorations = new List<Decoration>()
            {
                wallOutlet
            };

            prng = new System.Random(seedToUse);

            SquareChunk chunk = new SquareChunk(Vector2.zero, meshLength, 1, defaultMaterial, chunk => { GenerateLobbyLevel(chunk, Vector2.zero); });
            chunk.gameObject.GetComponent<MeshRenderer>().material = carpet;
        }
    }
}
