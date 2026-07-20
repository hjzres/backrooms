using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using NaughtyAttributes;

namespace Assets.Scripts.Levels
{
    public class Lobby : MonoBehaviour
    {
        // Relative to xz plane where +z is UP and +x is RIGHT
        public enum Direction 
        {
            UP,
            RIGHT,
            DOWN,
            LEFT,
            NONE
        }

        [SerializeField] [Min(1)] private uint seed;

        [Header("Generation Settings")]

        [SerializeField] private Material gray;

        [SerializeField] [Min(1)] private int segments, chanceThreshold;

        [SerializeField] [MinMaxSlider(0f, 1f)] private Vector2 pointSpawnChance;

        [SerializeField] [MinMaxSlider(1, 20)] private Vector2Int wallChainRange;

        [SerializeField] [Min(0.1f)] private float width = 1f;

        [Header("Gizmo Settings")]

        [SerializeField] private bool drawGizmos;

        [SerializeField] [Min(0.1f)] private float gizmoRadius;

        [SerializeField] private Color gizmoColor;

        private GameObject currentTestWall;

        private static readonly float3[] NormalizedDirections = new float3[5] 
        { 
            new(0, 0, 1),  // UP
            new(1, 0, 0),  // RIGHT
            new(0, 0, -1), // DOWN
            new(-1, 0, 0), // LEFT
            float3.zero   // NONE
        };

        // Vertices are created in a clockwise direction starting with the bottom left and ending with the bottom right.
        // Offset values follow the order aforementioned. x is bottom left, y is top left, z is top right, w is bottom right.
        private static float4 VertexOffsetTable(Direction lastDirection, Direction nextDirection, float width)
        {
            return (lastDirection, nextDirection) switch
            {
                (Direction.UP, Direction.UP) => new(-width, 0f, width, 0f),
                (Direction.UP, Direction.RIGHT) => new(-width, width, width, -width),
                (Direction.UP, Direction.LEFT) => new(-width, -width, width, width),
                (Direction.UP, Direction.NONE) => new(-width, 0f, width, 0f),

                (Direction.RIGHT, Direction.UP) => new(-width, width, width, -width),
                (Direction.RIGHT, Direction.RIGHT) => new(0f, width, 0f, -width),
                (Direction.RIGHT, Direction.DOWN) => new(width, width, -width, -width),
                (Direction.RIGHT, Direction.NONE) => new(0f, width, 0f, -width),

                (Direction.DOWN, Direction.RIGHT) => new(width, width, -width, -width),
                (Direction.DOWN, Direction.DOWN) => new(width, 0f, -width, 0f),
                (Direction.DOWN, Direction.LEFT) => new(width, -width, -width, width),
                (Direction.DOWN, Direction.NONE) => new(width, 0f, -width, 0f),

                (Direction.LEFT, Direction.UP) => new(-width, -width, width, width),
                (Direction.LEFT, Direction.DOWN) => new(width, -width, -width, width),
                (Direction.LEFT, Direction.LEFT) => new(0f, -width, 0f, width),
                (Direction.LEFT, Direction.NONE) => new(0f, -width, 0f, width),

                (Direction.NONE, Direction.NONE) => float4.zero,

                _ => throw new ArgumentException("Error looking up vertex offset. Method: [VertexOffsetLookup()].")
            };
        }

        [BurstCompile]
        private struct SegmentBuilder : IJob
        {
            [WriteOnly] public NativeArray<float3> vertices;

            [WriteOnly] public NativeArray<float2> uvs;

            [WriteOnly] public NativeArray<int> triangles;

            public float3 position;

            public float width;

            public Unity.Mathematics.Random prng;

            public int maxWalls;

            struct Point
            {
                public float3 position;

                public NativeArray<Direction> directions;

                public Point(float3 position, int maxDirections, ref Unity.Mathematics.Random prng)
                {
                    this.position = position;
                    directions = new(maxDirections + 1, Allocator.Temp);

                    Direction lastDir = Direction.NONE;

                    for (int i = 0; i < maxDirections; i++)
                    {
                        directions[i] = RandomizeDirection(lastDir, ref prng);
                        lastDir = directions[i];
                    }

                    directions[maxDirections] = Direction.NONE;
                }

                private static Direction RandomizeDirection(Direction lastDirection, ref Unity.Mathematics.Random prng)
                {
                    int num = prng.NextInt(0, 4);

                    return num switch
                    {
                        0 => lastDirection == Direction.DOWN ? Direction.DOWN : Direction.UP,
                        1 => lastDirection == Direction.LEFT ? Direction.LEFT : Direction.RIGHT,
                        2 => lastDirection == Direction.UP ? Direction.UP : Direction.DOWN,
                        3 => lastDirection == Direction.RIGHT ? Direction.RIGHT : Direction.LEFT,

                        _ => throw new ArgumentException("An error occured choosing a random direction for the next wall."),
                    };
                }
            }

            public void Execute()
            {
                Point point = new(float3.zero, maxWalls, ref prng);

                CreateOriginVerts(point.directions[0], position.x, position.z, ref vertices);
                SetTriangleQuad(0, 0, 1, 3, 2);

                int verts = 4;
                int tris = 6;

                for (int i = 0; i <= point.directions.Length - 2; i++)
                {
                    int distance = 5;

                    Direction lastDir = point.directions[i];
                    Direction nextDir = point.directions[i + 1];

                    position += NormalizedDirections[(int)lastDir] * distance;

                    float4 offsets = VertexOffsetTable(lastDir, nextDir, width);

                    vertices[verts] = new float3(position.x + offsets.x, 0, position.z + offsets.y);
                    vertices[verts + 1] = new float3(position.x + offsets.x, 5, position.z + offsets.y);
                    vertices[verts + 2] = new float3(position.x + offsets.z, 5, position.z + offsets.w);
                    vertices[verts + 3] = new float3(position.x + offsets.z, 0, position.z + offsets.w);

                    SetTriangleQuad(tris, verts, verts + 1, verts - 4, verts - 3);
                    SetTriangleQuad(tris + 6, verts + 3, verts - 1, verts + 2, verts - 2);

                    verts += 4;
                    tris += 12;
                }

                SetTriangleQuad(tris, verts - 2, verts - 3, verts - 1, verts - 4);

                if (point.directions.IsCreated)
                {
                    point.directions.Dispose();
                }
            }

            private void SetTriangleQuad(int index, int v0, int v1, int v2, int v3)
            {
                triangles[index] = v0;
                triangles[index + 1] = v1;
                triangles[index + 2] = v2;
                triangles[index + 3] = v3;
                triangles[index + 4] = v2;
                triangles[index + 5] = v1;
            }

            private readonly void CreateOriginVerts(Direction initialDir, float x, float z, ref NativeArray<float3> vertices)
            {
                switch (initialDir)
                {
                    case Direction.UP:
                        vertices[0] = math.float3(x - width, 0, z - width);
                        vertices[1] = math.float3(x - width, 5, z - width);
                        vertices[2] = math.float3(x + width, 5, z - width);
                        vertices[3] = math.float3(x + width, 0, z - width);
                        break;

                    case Direction.RIGHT:
                        vertices[0] = math.float3(x - width, 0, z + width);
                        vertices[1] = math.float3(x - width, 5, z + width);
                        vertices[2] = math.float3(x - width, 5, z - width);
                        vertices[3] = math.float3(x - width, 0, z - width);
                        break;

                    case Direction.DOWN:
                        vertices[0] = math.float3(x + width, 0, z + width);
                        vertices[1] = math.float3(x + width, 5, z + width);
                        vertices[2] = math.float3(x - width, 5, z + width);
                        vertices[3] = math.float3(x - width, 0, z + width);
                        break;

                    case Direction.LEFT:
                        vertices[0] = math.float3(x + width, 0, z - width);
                        vertices[1] = math.float3(x + width, 5, z - width);
                        vertices[2] = math.float3(x + width, 5, z + width);
                        vertices[3] = math.float3(x + width, 0, z + width);
                        break;
                }
            }
        }

        [Button]
        public void GenerateSimpleSegment() 
        {
            seed = (uint)DateTime.Now.Ticks;
            Unity.Mathematics.Random prng = new(seed);

            int maxWalls = prng.NextInt(wallChainRange.x + 1, wallChainRange.y + 1);

            NativeArray<float3> vertsArr = new((maxWalls + 1) * 4, Allocator.TempJob);
            NativeArray<float2> uvsArr = new((maxWalls + 1) * 4, Allocator.TempJob);
            NativeArray<int> trisArr = new((maxWalls + 1) * 12, Allocator.TempJob);

            SegmentBuilder job = new()
            {
                vertices = vertsArr,
                uvs = uvsArr,
                triangles = trisArr,
                position = float3.zero,
                width = width,
                prng = prng,
                maxWalls = maxWalls,
            };

            job.Schedule().Complete();

            Vector3[] vertices = new Vector3[(maxWalls + 1) * 4];
            Vector2[] uvs = new Vector2[(maxWalls + 1)  * 4];
            int[] triangles = new int[(maxWalls + 1) * 12];

            vertsArr.Reinterpret<Vector3>().CopyTo(vertices);
            uvsArr.Reinterpret<Vector2>().CopyTo(uvs);
            trisArr.CopyTo(triangles);

            vertsArr.Dispose();
            uvsArr.Dispose();
            trisArr.Dispose();

            Mesh mesh = new()
            {
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            GameObject obj = new("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            obj.GetComponent<MeshFilter>().mesh = mesh;
            obj.GetComponent<MeshRenderer>().material = gray;

            if (currentTestWall != null)
            {
                DestroyImmediate(currentTestWall);
            }

            currentTestWall = obj;
        }

        //[Button]
        //public void GenerateMaze()
        //{
        //    Chunk chunk = new(float2.zero, 50, 5, gray);
        //    //float2 chunkBL = new(chunk.position.x - (0.5f * chunk.length), chunk.position.y - (0.5f * chunk.length));
        //    float2 chunkBL = new(chunk.position.x, chunk.position.y);

        //    seed = (uint)DateTime.Now.Ticks;
        //    Unity.Mathematics.Random prng = new(seed);

        //    float spacing = (float)chunk.length / segments, chance = 0f;
        //    NativeList<float2> originPoints = new();

        //    for (int x = 0; x <= segments; x++)
        //    {
        //        for (int y = 0; y <= segments; y++)
        //        {
        //            if ((x == 0 || x == segments || y == 0 || y == segments) && chance > pointSpawnChance.x)
        //            {
        //                chance -= pointSpawnChance.x;
        //                continue;
        //            }

        //            chance += prng.NextFloat(pointSpawnChance.x, pointSpawnChance.y);

        //            if (chance >= chanceThreshold)
        //            {
        //                float2 originPosition = new(chunkBL.x + (x * spacing), chunkBL.y + (y * spacing));
        //                originPoints.Add(originPosition);

        //                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        //                cube.transform.position = new Vector3(originPosition.x, 0, originPosition.y);
        //                cube.transform.parent = chunk.transform;

        //                chance = 0;
        //            }
        //        }
        //    }
        //}
    }
}
