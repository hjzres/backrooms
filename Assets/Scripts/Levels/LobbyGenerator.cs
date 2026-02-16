using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using static Assets.Scripts.Chunks;

namespace Assets.Scripts.Levels
{
    public enum NextDirection
    {
        NONE, UP, DOWN, LEFT, RIGHT
    }

    public class LobbyGenerator : MonoBehaviour
    {
        [SerializeField] private Transform player;

        [SerializeField] [Min(1)] private uint seed;

        [Header("Chunk Properties")]

        [SerializeField] private Material chunkMaterial;

        [SerializeField] [Min(1)] private int resolution;

        [SerializeField] private int length = 100;

        [SerializeField] private int viewDistanceInChunks = 3;

        [Header("Generation Properties")]

        [SerializeField] private Material grayMaterial;

        [SerializeField] private Material wallMaterial;

        [SerializeField][Range(1, 20)] private int segments = 18;

        [SerializeField] private float chanceThreshold = 4.0f;

        [SerializeField][Min(1)] private float wallHeight = 7.0f;

        [SerializeField] private Vector2 pointSpawnChance = new(0.1f, 1);

        [SerializeField] private Vector2 wallChainRange = new(3, 7);

        [SerializeField] private Vector2 wallLengthRange = new(3, 8);

        [SerializeField] private int ceilingLightNumber = 8;

        // ------------------------------------------------------------------------------------------- //

        private Dictionary<Vector2Int, Chunk> chunks;

        private GameObject chunkContainer;

        private Unity.Mathematics.Random prng;

        // ------------------------------------------------------------------------------------------- //

        private readonly struct Directions
        {
            public static readonly float3 UP_v = new(0, 0, 1);

            public static readonly float3 DOWN_v = new(0, 0, -1);

            public static readonly float3 LEFT_v = new(-1, 0, 0);

            public static readonly float3 RIGHT_v = new(1, 0, 0);
        }

        private struct Point
        {
            public float3 nextPosition;

            public NextDirection nextDirection;

            public readonly NextDirection RandomizeDirection(Unity.Mathematics.Random prng)
            {
                switch (prng.NextInt(1, sizeof(NextDirection) + 1))
                {
                    case 1:
                        return NextDirection.UP;

                    case 2:
                        return NextDirection.DOWN;

                    case 3:
                        return NextDirection.LEFT;

                    case 4:
                        return NextDirection.RIGHT;

                    default:
                        break;
                }

                return NextDirection.NONE;
            }
        }

        private void Awake()
        {
            chunks = new Dictionary<Vector2Int, Chunk>();
            prng = new Unity.Mathematics.Random(seed);
            chunkContainer = new GameObject("Generated Chunks");
        }

        private void LateUpdate()
        {
            UpdateChunks();
        }

        private void GenerateBehaviour(Chunk chunk, Vector2 coordinates)
        {
            Vector2 chunkPosition = new(chunk.position.x, chunk.position.y);
            GameObject tempLightContainer = new();
            GameObject tempWallContainer = new();

            tempLightContainer.transform.SetParent(chunk.transform);
            tempWallContainer.transform.SetParent(chunk.transform);

            List<MeshFilter> wallBatchList = new List<MeshFilter>();
            List<MeshFilter> lightBatchList = new List<MeshFilter>();

            float noise = Noise.WhiteNoise2D(new Vector2(coordinates.x + seed, coordinates.y + seed)) * 100;

            if (noise < 50)
            {
                List<Point> startPoints = new();

                RandomizeStartingPoints(chunkPosition, chunk.length, startPoints);
                CreateWalls(tempWallContainer.transform, startPoints, wallBatchList);
                //AddCeilingLights(200, chunk, chunkPosition, tempLightContainer.transform, lightBatchList);

                ReduceMeshCount(tempWallContainer, chunk.transform, "Maze Walls", wallMaterial, wallBatchList);
                //ReduceMeshCount(tempLightContainer, chunk.transform, "Ceiling Lights", grayMaterial, lightBatchList);
            }

            else
            {
                return;
            }
        }

        private void RandomizeStartingPoints(Vector2 chunkPosition, int length, List<Point> startPoints)
        {
            NativeArray<Point> startPoints_array = new(50, Allocator.Persistent);

            StartPointsJob job = new() 
            { 
                start_points = startPoints_array, 
                chunk_position = chunkPosition, 
                segments = segments, 
                chance_threshold = chanceThreshold, 
                point_spawn_chance = pointSpawnChance, 
                wall_height = wallHeight, 
                length = length, 
                PRNG = prng 
            };

            JobHandle handle = job.Schedule();
            handle.Complete();

            for (int i = 0; i < startPoints_array.Length; i++)
            {
                if (math.all(startPoints_array[i].nextPosition != 0))
                {
                    startPoints.Add(startPoints_array[i]);
                }
            }

            startPoints_array.Dispose(handle);
        }

        private void CreateWalls(Transform parent, List<Point> startPoints, List<MeshFilter> batchingList)
        {
            if (startPoints.Count <= 0)
            {
                throw new ArgumentException("There are no { startPoints } available! [Method: CreateWalls()].");
            }

            NativeArray<Point> startPoints_array = new(startPoints.Count, Allocator.Persistent);
            NativeArray<float3> positions_array = new(300, Allocator.Persistent);
            NativeArray<float3> scales_array = new(300, Allocator.Persistent);

            for (int i = 0; i < startPoints.Count; i++)
            {
                startPoints_array[i] = startPoints[i];
            }

            MazeWallJob job = new()
            {
                start_points = startPoints_array, 
                positions = positions_array, 
                scales = scales_array, 
                wall_chain_range = wallChainRange, 
                wall_length_range = wallLengthRange, 
                wall_height = wallHeight, 
                PRNG = prng
            };

            JobHandle handle = job.Schedule();
            handle.Complete();

            for (int i = 0; i < scales_array.Length; i++)
            {
                if (math.all(scales_array[i] != 0))
                {
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = positions_array[i];
                    wall.transform.localScale = scales_array[i];
                    wall.transform.parent = parent;
                    wall.GetComponent<MeshRenderer>().material = wallMaterial;
                    wall.layer = 6;

                    batchingList.Add(wall.GetComponent<MeshFilter>());
                }
            }

            startPoints_array.Dispose(handle);
            positions_array.Dispose(handle);
            scales_array.Dispose(handle);
            startPoints.Clear();
        }

        private async void AddCeilingLights(int duration, Chunk chunk, Vector2 chunkPosition, Transform parent, List<MeshFilter> batchingList)
        {
            await Task.Delay(duration);

            float spacing = (float)chunk.length / ceilingLightNumber;

            for (int x = 0; x < ceilingLightNumber; x++)
            {
                for (int y = 0;  y < ceilingLightNumber; y++)
                {
                    Vector3 position = new(chunkPosition.x + x * spacing, wallHeight, chunkPosition.y + y * spacing);
                    Vector3 sphereCastPosition = position + new Vector3(0, 5, 0);

                    if (!Physics.SphereCast(sphereCastPosition, 2f, Vector3.down.normalized, out _, wallHeight * 0.75f, LayerMask.GetMask("Environment")))
                    {
                        GameObject light = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        light.transform.position = position;
                        light.transform.SetParent(parent);
                        batchingList.Add(light.GetComponent<MeshFilter>());
                    }
                }
            }
        }

        private void ReduceMeshCount(GameObject tempContainer, Transform parent, string meshName, Material material, List<MeshFilter> batchingList)
        {
            var combineInstance = new CombineInstance[batchingList.Count];
            GameObject combinedMesh = new(meshName, typeof(MeshFilter), typeof(MeshRenderer));

            for (int i = 0; i < batchingList.Count; i++)
            {
                combineInstance[i].mesh = batchingList[i].sharedMesh;
                combineInstance[i].transform = batchingList[i].transform.localToWorldMatrix;
            }

            Mesh mesh = new();
            mesh.CombineMeshes(combineInstance);

            combinedMesh.GetComponent<MeshFilter>().sharedMesh = mesh;
            combinedMesh.GetComponent<MeshRenderer>().material = material;
            combinedMesh.AddComponent<MeshCollider>();
            combinedMesh.transform.SetParent(parent);
            combinedMesh.isStatic = true;

            DestroyObject(tempContainer);
            batchingList.Clear();
        }

        private static void DestroyObject(GameObject gameObject)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }

            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void UpdateChunks()
        {
            Vector2Int clientChunkCoord = ComputeCliendChunkCoords(player.position, length);

            for (int x = -viewDistanceInChunks; x <= viewDistanceInChunks; x++)
            {
                for (int y = -viewDistanceInChunks; y <= viewDistanceInChunks; y++)
                {
                    Vector2Int coordinates = new(clientChunkCoord.x + x, clientChunkCoord.y + y);

                    if (!chunks.ContainsKey(coordinates))
                    {
                        Vector2 chunkCoordinates = coordinates * length;
                        Chunk chunk = new(chunkCoordinates, resolution, length, chunkMaterial, chunk => GenerateBehaviour(chunk, chunkCoordinates));
                        chunk.transform.parent = chunkContainer.transform;
                        chunks.Add(coordinates, chunk);
                    }
                }
            }

            // Done by AI, will improve this later.
            foreach (var chunk in chunks)
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

        [BurstCompile]
        private struct StartPointsJob : IJob
        {
            public NativeArray<Point> start_points;

            public float2 chunk_position;

            public int segments;

            public float chance_threshold;

            public float wall_height;

            public float2 point_spawn_chance;

            public int length;

            public Unity.Mathematics.Random PRNG;

            public void Execute()
            {
                float spacing = (float)length / segments;
                float chance = 0f;

                int i = 0;

                for (int x = 0; x <= segments; x++)
                {
                    for (int y = 0; y <= segments; y++)
                    {
                        if ((x == 0 || x == segments || y == 0 || y == segments) && chance > point_spawn_chance.x)
                        {
                            chance -= point_spawn_chance.x;
                            continue;
                        }

                        chance += PRNG.NextFloat(point_spawn_chance.x, point_spawn_chance.y);

                        if (chance >= chance_threshold)
                        {
                            float posX = chunk_position.x + (x * spacing);
                            float posZ = chunk_position.y + (y * spacing);

                            Point point = new() { nextPosition = new Vector3(posX, wall_height * 0.5f, posZ) };

                            start_points[i] = point;
                            chance = 0f;
                            i++;
                        }
                    }
                }
            }
        }

        [BurstCompile]
        private struct MazeWallJob : IJob
        {
            public NativeArray<Point> start_points;

            public NativeArray<float3> positions;

            public NativeArray<float3> scales;

            public float2 wall_chain_range;

            public float2 wall_length_range;

            public float wall_height;

            public Unity.Mathematics.Random PRNG;

            public void Execute()
            {
                int k = 0;

                for (int i = 0; i < start_points.Length; i++)
                {
                    int iterations = PRNG.NextInt((int)wall_chain_range.x, (int)wall_chain_range.y + 1);

                    Point point = start_points[i];
                    float3 previousDirection = new(0, 0, 0);

                    for (int j = 0; j < iterations; j++)
                    {
                        point.nextDirection = point.RandomizeDirection(PRNG);
                        float3 direction = GetDirectionForNextWall(point, previousDirection);

                        int length = PRNG.NextInt((int)wall_length_range.x, (int)wall_length_range.y);

                        // Adding +1 simplifies the code and overlaps the walls, but due to the material it will not be noticeable.
                        int xAxisScale = point.nextDirection == NextDirection.LEFT || point.nextDirection == NextDirection.RIGHT ? length + 1 : 1;
                        int zAxisScale = point.nextDirection == NextDirection.UP || point.nextDirection == NextDirection.DOWN ? length + 1 : 1;

                        previousDirection = direction;

                        positions[k] = point.nextPosition + 0.5f * length * direction;
                        scales[k] = new Vector3(xAxisScale, wall_height, zAxisScale);
                        k++;

                        point.nextPosition += direction * length;
                    }
                }
            }

            private readonly float3 GetDirectionForNextWall(Point point, float3 previousDirection)
            {
                float3 direction = new();

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

                    default:
                        break;
                }

                return math.all(direction == -previousDirection) ? -direction : direction;
            }
        }

        [Button]
        public void GenerateTestChunk()
        {
            prng = new Unity.Mathematics.Random(seed);

            Chunk chunk = new(float2.zero, resolution, length, chunkMaterial, chunk => GenerateBehaviour(chunk, Vector2.zero));
        }
    }
}
