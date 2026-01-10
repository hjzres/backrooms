using System;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using static Assets.Scripts.Chunks;
using NUnit.Framework.Internal;

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
        [SerializeField] private Transform player;

        [Header("Chunking Properties")]

        [SerializeField] [Min(1)] private int meshLength = 1;

        [SerializeField] [Min(1)] private int resolution = 1;

        [SerializeField] private int clientDistanceInChunks = 1;

        private GameObject chunkContainer;

        private static Dictionary<Vector2Int, SquareChunk> squareChunks;

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

        [Header("Materials")]

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

        private struct Wall
        {
            public GameObject gameObject;

            private readonly Action<Wall> onCreate;

            public Wall(Material material, Action<Wall> onCreate)
            {
                gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gameObject.GetComponent<MeshRenderer>().material = material;
                this.onCreate = onCreate;
                onCreate?.Invoke(this);
            }
        }

        [System.Serializable]
        public struct Decoration
        {
            public string name;

            public GameObject gameObject;

            public GameObject prefab;

            public int maxSpawnChance;

            public float rotationOffsetY;

            public float positionOffsetY;

            public float offsetReductionXZ;
        }

        [Header("Wall Decorations")]
        public Decoration wallOutlet;

        private List<Decoration> decorations;

        private void Awake()
        {
            squareChunks = new Dictionary<Vector2Int, SquareChunk>();

            chunkContainer = new GameObject("Generated Chunks");
            prng = new System.Random(seed);

            AddDecorationsToList();
        }

        // Unfortunately has to be hard coded.
        private void AddDecorationsToList()
        {
            decorations = new List<Decoration>()
            {
                wallOutlet
            };
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

                    Vector3 wallPosition = point.nextPosition + direction * length * 0.5f;

                    // Have a chance to spawn one of the decorations from the list.
                    int decorationIndex = prng.Next(0, decorations.Count);
                    float scale = Mathf.Max(xAxisScale, zAxisScale);

                    Wall wall = new Wall(arrowWallpaper, onCreate => AddDecorationsOnWall(decorationIndex, scale, wallPosition, previousDirection, chainParent.transform));
                    wall.gameObject.transform.position = wallPosition;
                    wall.gameObject.transform.localScale = new Vector3(xAxisScale, wallHeight, zAxisScale);

                    point.nextPosition += direction * length;

                    wall.gameObject.transform.parent = chainParent.transform;

                    if (useTestVisuals)
                    {
                        GameObject otherRefPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        otherRefPoint.transform.position = point.nextPosition;
                        otherRefPoint.transform.parent = chainParent.transform;
                    }
                }
            }

            startPoints.Clear();
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

        private void AddDecorationsOnWall(int index, float wallScale, Vector3 wallPosition, Vector3 direction, Transform parent)
        {
            bool generationChance = prng.Next(1, wallOutlet.maxSpawnChance) == 1;

            if (!generationChance) return;

            Decoration deco = decorations[index];
            //deco.prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallScale *= 0.5f;

            GameObject decoObj = Instantiate(deco.prefab);

            int side = prng.Next(1, 10) < 5 ? -1 : 1;

            float offsetX = direction == Directions.UP_v || direction == Directions.DOWN_v ? 0.5f * side: 0;
            float offsetZ = direction == Directions.LEFT_v || direction == Directions.RIGHT_v ? -0.5f * side: 0;

            float randOffsetZ = offsetZ == 0 ? NextFloat(-wallScale + deco.offsetReductionXZ, wallScale - deco.offsetReductionXZ) : 0;
            float randOffsetX = offsetX == 0 ? NextFloat(-wallScale + deco.offsetReductionXZ, wallScale - deco.offsetReductionXZ) : 0;

            Vector3 offsets = new Vector3(offsetX, wallOutlet.positionOffsetY, offsetZ + randOffsetZ);

            float rotX = offsetX == 0 ? 90 : (offsetZ == 0 ? (side == -1 ? 180 : 0) : 0);
            float rot = offsetZ == 0 ? (side == -1 ? 180 : 0) : (side == -1 ? 270 : 90);

            decoObj.transform.rotation = Quaternion.Euler(deco.prefab.transform.rotation.x, deco.prefab.transform.rotation.y + rot, deco.prefab.transform.rotation.z);
            decoObj.transform.position = wallPosition + offsets;
            decoObj.transform.parent = parent;
        }

        private void UpdateClientChunks()
        {
            // chunk coords
            Vector2Int clientChunkCoord = ComputeCliendChunkCoords(player.position, meshLength);

            for (int x = -clientDistanceInChunks; x <= clientDistanceInChunks; x++)
            {
                for (int y = -clientDistanceInChunks; y <= clientDistanceInChunks; y++)
                {
                    Vector2Int coordinates = new Vector2Int(clientChunkCoord.x + x, clientChunkCoord.y + y);

                    if (!squareChunks.ContainsKey(coordinates))
                    {
                        Vector2 chunkPosition = coordinates * meshLength;
                        SquareChunk chunk = new SquareChunk(chunkPosition, meshLength, 1, chunk => { GenerateLobbyMaze(chunk); });
                        chunk.gameObject.GetComponent<MeshRenderer>().material = carpet;
                        chunk.gameObject.transform.parent = chunkContainer.transform;

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
                bool isWithinRange = Mathf.Abs(delta.x) <= clientDistanceInChunks && Mathf.Abs(delta.y) <= clientDistanceInChunks; 
                chunk.Value.gameObject.SetActive(isWithinRange); 
            }
        }

        private Vector2Int ComputeCliendChunkCoords(Vector3 clientPosition, int chunkLength)
        {
            float operationX = clientPosition.x / chunkLength;
            float operationY = clientPosition.z / chunkLength;

            return new Vector2Int(Mathf.RoundToInt(operationX), Mathf.RoundToInt(operationY));
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
            AddDecorationsToList();

            prng = new System.Random(seedToUse);

            SquareChunk chunk = new SquareChunk(new Vector2(0, 0), meshLength, resolution, chunk => { GenerateLobbyMaze(chunk); });
            chunk.gameObject.GetComponent<MeshRenderer>().material = carpet;
        }
    }
}
