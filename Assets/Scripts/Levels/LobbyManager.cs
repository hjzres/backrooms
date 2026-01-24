using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using static Assets.Scripts.Chunks;

namespace Assets.Scripts.Levels
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

            public LobbyWall(Vector3 position, Vector3 scale, List<MeshFilter> batchingList, Action<LobbyWall> onCreate)
            {
                gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                transform = gameObject.transform;
                transform.position = position;
                transform.localScale = scale;

                this.onCreate = onCreate;
                onCreate?.Invoke(this);

                batchingList.Add(gameObject.GetComponent<MeshFilter>());
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

        public TransformAccessArray _accessArray;

        [BurstCompile]
        private struct UpdateClientChunksJob : IJobParallelForTransform
        {
            public int _viewDistanceInChunks;

            public float3 _playerPosition;

            public int _meshLength;

            public void Execute(int index, TransformAccess transform)
            {
                float2 clientChunkCoord = ComputeClientChunkCoords();

                for (int x = -_viewDistanceInChunks; x <= _viewDistanceInChunks; x++)
                {
                    for (int y = -_viewDistanceInChunks; y <= _viewDistanceInChunks; y++)
                    {
                        float2 coordinates = new float2(clientChunkCoord.x + x, clientChunkCoord.y + y);
                    }
                }
            }

            private float2 ComputeClientChunkCoords()
            {
                int operationX = Mathf.RoundToInt(_playerPosition.x / _meshLength);
                int operationY = Mathf.RoundToInt(_playerPosition.z / _meshLength);

                return new float2(operationX, operationY);
            }
        }

        // ------------------------------------------------------------------------------------------- //

        [SerializeField] private Transform player;

        [Header("Chunking Properties")]

        [SerializeField] [Min(1)] private int meshLength = 1;

        [SerializeField] [Min(1)] private int viewDistanceInChunks = 1;

        [Space(15f)]

        [SerializeField] private int mazeSpawnChance = 85;

        [SerializeField] private int repetitiveWallsSpawnChance = 10;

        [SerializeField] private int pitfallsSpawnChance = 5;

        [SerializeField] private LayerMask lobbyWallMask; // DOESNT WORK TO SET ON WALL RN

        [Header("Generation Properties")]

        [SerializeField] private bool useRandomSeed = false;

        [Space(15f)]

        [SerializeField] private int seed; // MULTIPLAYER ONLY NEEDS THIS SENT TO CLIENTS.

        [SerializeField] [Range(1, 20)] private int segments = 18;

        [SerializeField] private float chanceThreshold = 4.0f;

        [SerializeField] [Min(1)] private float wallHeight = 7.0f;

        [SerializeField] private Vector2 pointSpawnChance = new Vector2(0.1f, 1);

        [SerializeField] private Vector2Int wallChainRange = new Vector2Int(3, 7);
        
        [SerializeField] private Vector2Int wallLengthRange = new Vector2Int(3, 8);

        [SerializeField] [Min(1)] private int repetitiveWallSegments = 14;

        [SerializeField] private float repetitiveWallsThickness = 1.5f;

        [SerializeField] [Min(1)] private int pitfallNumber = 8;

        [SerializeField] [Min(1)] private float pitfallThickness = 2;

        [SerializeField] [Min(0)] private int pitfallDepth = 80;

        [Header("Materials")]

        [SerializeField] private Material defaultMaterial;

        [SerializeField] private Material arrowWallpaper;

        [SerializeField] private Material carpet;

        [Header("Lighting & Decorations")]

        [SerializeField] private int lightNumber;

        [SerializeField] private GameObject lightModelPrefab;

        [SerializeField] private GameObject pointLightPrefab;

        [SerializeField] private Decoration wallOutlet; // 13, -2, 0.

        private readonly float decorationSphereCastRadius = 1.0f;

        private readonly float decorationSphereCastDistance = 5f;

        private readonly float lightSphereCastRadius = 1f;

        private readonly float lightSphereCastDistance = 5f;

        // ------------------------------------------------------------------------------------------- //

        private GameObject generatedChunksContainer;

        private static Dictionary<Vector2Int, SquareChunk> squareChunks;

        //private static NativeHashMap<float2, SquareChunk> squareChunksNEW;

        private System.Random prng; // MUST BE CREATED ONCE.

        private List<Point> startPoints;

        private List<Decoration> decorations;

        // ------------------------------------------------------------------------------------------- //

        private void Awake()
        {
            squareChunks = new Dictionary<Vector2Int, SquareChunk>();
            startPoints = new List<Point>();

            generatedChunksContainer = new GameObject("Generated Chunks");
            prng = new System.Random(seed);

            decorations = new List<Decoration>() { wallOutlet };
        }

        private void LateUpdate()
        {
            UpdateClientChunks();
        }

        private void GenerateLobbyLevel(SquareChunk chunk, Vector2 coordinates)
        {
            List<MeshFilter> wallMeshesToCombineList = new List<MeshFilter>();
            List<MeshFilter> lightMeshesToCombineList = new List<MeshFilter>();

            GameObject tempWallContainer = new GameObject("Temporary Wall Container");
            GameObject tempLightContainer = new GameObject("Temporary Light Container");

            float noise = Noise.WhiteNoise2D(new Vector2(coordinates.x + seed, coordinates.y + seed)) * 100;
            Vector2 bottomLeft = new Vector2(chunk.position.x - chunk.length * 0.5f, chunk.position.y - chunk.length * 0.5f);

            SquareChunk ceiling = new SquareChunk(coordinates, meshLength, 1, defaultMaterial, null);
            ceiling.gameObject.name = "Ceiling";
            ceiling.transform.SetPositionAndRotation(new Vector3(ceiling.transform.position.x, wallHeight, ceiling.transform.position.z + meshLength), Quaternion.Euler(new Vector3(180, 0, 0)));
            ceiling.transform.parent = chunk.transform;
            ceiling.gameObject.isStatic = true;

            if (noise < mazeSpawnChance)
            {
                chunk.ID = (int)ChunkID.MAZE;
                startPoints.Clear();

                RandomizeStartingPoints(chunk, bottomLeft);
                CreateWalls(tempWallContainer.transform, wallMeshesToCombineList);

                AddLightsToCeiling(chunk, bottomLeft, tempLightContainer.transform, lightMeshesToCombineList);
            }

            else if (noise >= mazeSpawnChance && noise < mazeSpawnChance + repetitiveWallsSpawnChance)
            {
                chunk.ID = (int)ChunkID.REPETITIVE;
                GenerateRepetitiveWalls(chunk, bottomLeft, tempWallContainer.transform, wallMeshesToCombineList);
            }

            else if (noise >= repetitiveWallsSpawnChance && noise <= mazeSpawnChance + repetitiveWallsSpawnChance + pitfallsSpawnChance)
            {
                chunk.ID = (int)ChunkID.PITFALL;
                GeneratePitfalls(chunk, bottomLeft, tempWallContainer.transform, wallMeshesToCombineList);
            }

            StartCoroutine(ReduceMeshCount(chunk, tempWallContainer, wallMeshesToCombineList, ""));
            StartCoroutine(ReduceMeshCount(chunk, tempLightContainer, lightMeshesToCombineList, UnityCoreData.lightTag));
        }

        private void RandomizeStartingPoints(SquareChunk chunk, Vector2 bottomLeft)
        {
            float spacing = (float)chunk.length / segments;
            float chance = 0f;

            for (int x = 0; x <= segments; x++)
            {
                for (int y = 0; y <= segments; y++)
                {
                    if ((x == 0 || x == segments || y == 0 || y == segments) && chance > pointSpawnChance.x)
                    {
                        chance -= pointSpawnChance.x;
                        continue;
                    }

                    chance += NextFloat(pointSpawnChance.x, pointSpawnChance.y);

                    if (chance >= chanceThreshold)
                    {
                        float posX = bottomLeft.x + (x * spacing);
                        float posZ = bottomLeft.y + (y * spacing);

                        Point point = new Point { nextPosition = new Vector3(posX, wallHeight * 0.5f, posZ) };

                        startPoints.Add(point);
                        chance = 0f;
                    }
                }
            }
        }

        private void CreateWalls(Transform parent, List<MeshFilter> batchingList)
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

                    LobbyWall wall = new LobbyWall(position, scale, batchingList, onCreate => { AddDecorationsOnWall(decorationIndex, scaleInRespectToDirection, position, previousDirection, parent, batchingList); });
                    wall.transform.parent = parent;
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

        private void AddDecorationsOnWall(int decorationIndex, float wallScaleFactor, Vector3 wallPosition, Vector3 wallDirection, Transform parent, List<MeshFilter> batchingList)
        {
            bool canSpawn = prng.Next(1, wallOutlet.spawnChance) == 1;

            if (!canSpawn) 
            { 
                return; 
            }

            Decoration deco = decorations[decorationIndex];

            int sideMultiplier = prng.Next(1, 10) < 5 ? -1 : 1;

            float offsetX = wallDirection == Directions.UP_v || wallDirection == Directions.DOWN_v ? 0.5f * sideMultiplier : 0;
            float offsetZ = wallDirection == Directions.LEFT_v || wallDirection == Directions.RIGHT_v ? -0.5f * sideMultiplier : 0;

            wallScaleFactor *= 0.4f; // Decrease random offset area.
            float randOffsetX = offsetX == 0 && deco.useRandomOffsets ? NextFloat(-wallScaleFactor + deco.offsetReductionXZ, wallScaleFactor - deco.offsetReductionXZ) : 0;
            float randOffsetZ = offsetZ == 0 && deco.useRandomOffsets ? NextFloat(-wallScaleFactor + deco.offsetReductionXZ, wallScaleFactor - deco.offsetReductionXZ) : 0;

            Vector3 offsets = new Vector3(offsetX + randOffsetX, deco.positionOffsetY, offsetZ + randOffsetZ);
            Vector3 sphereCastPosition = new Vector3(wallPosition.x + offsets.x * (decorationSphereCastRadius + 1), wallPosition.y + offsets.y, wallPosition.z + offsets.z * (decorationSphereCastRadius + 1));

            // All prefabs must be offset in the +x direction, with the main face looking in that direction and the origin at (0,0,0).
            float rotY = offsetZ == 0 ? (sideMultiplier == -1 ? 180 : 0) : (sideMultiplier == -1 ? 270 : 90);
            Vector3 localForward = rotY == 0 ? Directions.RIGHT_v : (rotY == 180 ? Directions.LEFT_v : (rotY == 270 ? Directions.DOWN_v : Directions.UP_v));

            // TODO: Every type of decoration should have its own batch.
            //StartCoroutine(SpherecastCollision(0.1f, sphereCastPosition, decorationSphereCastDistance, localForward, decorationSphereCastDistance, lobbyWallMask, () => PrefabToCopiedGameObject(null, wallPosition + offsets, new Vector3(0, rotY, 0), null, UnityCoreData.decorationTag, null)));
        }

        private void GenerateRepetitiveWalls(SquareChunk chunk, Vector2 bottomLeft, Transform parent, List<MeshFilter> batchingList)
        {
            float spacing = (float)chunk.length / repetitiveWallSegments;

            for (int x = 0; x <= repetitiveWallSegments; x++)
            {
                for (int y = 0; y <= repetitiveWallSegments; y++)
                {
                    Vector3 position = new Vector3(bottomLeft.x + x * spacing, wallHeight * 0.5f, bottomLeft.y + y * spacing);
                    Vector3 scale = new Vector3(repetitiveWallsThickness, wallHeight, repetitiveWallsThickness);

                    LobbyWall wall = new LobbyWall(position, scale, batchingList, null);
                    wall.transform.parent = parent;
                }
            }
        }

        private void GeneratePitfalls(SquareChunk chunk, Vector2 bottomLeft, Transform parent, List<MeshFilter> batchingList)
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
                        LobbyWall wallX = new LobbyWall(new Vector3(posX + offset, pitfallDepth * -0.5f, posZ), new Vector3(spacing, pitfallDepth, pitfallThickness), batchingList, null);
                        wallX.transform.parent = parent;
                    }

                    if (y < pitfallNumber)
                    {
                        LobbyWall wallZ = new LobbyWall(new Vector3(posX, pitfallDepth * -0.5f, posZ + offset), new Vector3(pitfallThickness, pitfallDepth, spacing), batchingList, null);
                        wallZ.transform.parent = parent;
                    }
                }
            }
        }

        private void AddLightsToCeiling(SquareChunk chunk, Vector2 bottomLeft, Transform parent, List<MeshFilter> batchingList)
        {
            float spacing = (float)chunk.length / lightNumber;

            for (int x = 0; x < lightNumber; x++)
            {
                for (int y = 0; y < lightNumber; y++)
                {
                    float posX = bottomLeft.x + x * spacing;
                    float posZ = bottomLeft.y + y * spacing;

                    Vector3 position = new Vector3(posX, wallHeight, posZ);
                    Vector3 castPosition = position + new Vector3(0, 5, 0);

                    StartCoroutine(SpherecastCollision(.1f, castPosition, lightSphereCastRadius, Vector3.down.normalized, lightSphereCastDistance, lobbyWallMask, () => PrefabToCopiedGameObject(lightModelPrefab, position, Vector3.zero, parent, UnityCoreData.lightTag, batchingList)));
                }
            }
        }

        private void PrefabToCopiedGameObject(GameObject prefab, Vector3 position, Vector3 eulerAngles, Transform parent = null, string tag = "", List<MeshFilter> batchingList = null)
        {
            GameObject copy = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            copy.transform.SetPositionAndRotation(position, Quaternion.Euler(eulerAngles));
            copy.transform.parent = parent;
            copy.tag = tag;

            batchingList?.Add(copy.GetComponent<MeshFilter>());
        }

        private IEnumerator SpherecastCollision(float delayTime, Vector3 castPosition, float radius, Vector3 castDirection, float maxDistance, LayerMask mask, Action logic)
        {
            yield return new WaitForSecondsRealtime(delayTime);

            RaycastHit hit = new RaycastHit();
            bool castHit = Physics.SphereCast(castPosition, radius, castDirection, out hit, maxDistance, mask);

            if (!castHit)
            {
                logic?.Invoke();
            }
        }

        // THERE IS A BOTTLENECK SOMEWHERE WHEN GENERATING FURTHER CHUNKS SO FIX THIS SOMETIME.
        private IEnumerator ReduceMeshCount(SquareChunk chunk, GameObject tempContainer, List<MeshFilter> batchingList, string tag)
        {
            yield return new WaitForSecondsRealtime(0.2f);

            if (batchingList.Count == 0 || batchingList == null)
            {
                if (Application.isPlaying)
                {
                    Destroy(tempContainer);
                }

                else
                {
                    DestroyImmediate(tempContainer);
                }

                yield break;
            }

            var combineInstance = new CombineInstance[batchingList.Count];

            string name = tag == UnityCoreData.lightTag ? "Light Meshes On Chunk" : "Wall Meshes On Chunk";
            GameObject combinedMesh = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));

            for (int i = 0; i < batchingList.Count; i++)
            {
                combineInstance[i].mesh = batchingList[i].sharedMesh;
                combineInstance[i].transform = batchingList[i].transform.localToWorldMatrix;

                if (tag == UnityCoreData.lightTag)
                {
                    GameObject light = Instantiate(pointLightPrefab);
                    light.transform.position = new Vector3(batchingList[i].transform.position.x, batchingList[i].transform.position.y - .5f, batchingList[i].transform.position.z);
                    light.transform.parent = combinedMesh.transform;
                }
            }

            Mesh mesh = new Mesh();
            mesh.CombineMeshes(combineInstance);

            Material material = tag == UnityCoreData.lightTag || tag == UnityCoreData.decorationTag ? defaultMaterial : arrowWallpaper;

            combinedMesh.GetComponent<MeshFilter>().sharedMesh = mesh;
            combinedMesh.GetComponent<MeshRenderer>().material = material;
            combinedMesh.AddComponent<MeshCollider>();
            combinedMesh.transform.parent = chunk.transform;
            combinedMesh.layer = tag == UnityCoreData.lightTag ? 0 : 4;
            combinedMesh.isStatic = true;

            if (Application.isPlaying) 
            { 
                Destroy(tempContainer); 
            }

            else 
            { 
                DestroyImmediate(tempContainer); 
            }

            batchingList.Clear();
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

        private void UpdateClientChunks1()
        {
            UpdateClientChunksJob job = new UpdateClientChunksJob()
            {
                _viewDistanceInChunks = viewDistanceInChunks,
                _meshLength = meshLength,
                _playerPosition = player.position
            };
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
            decorations = new List<Decoration>() { wallOutlet };
            startPoints = new List<Point>();

            prng = new System.Random(seedToUse);

            SquareChunk chunk = new SquareChunk(Vector2.zero, meshLength, 1, defaultMaterial, chunk => { GenerateLobbyLevel(chunk, Vector2.zero); });
            chunk.gameObject.GetComponent<MeshRenderer>().material = carpet;
        }
    }
}
