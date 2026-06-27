using NaughtyAttributes;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

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

        [SerializeField] [Min(0.1f)] private float thickness = 1f;

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
        private static float4 VertexOffsetTable(Direction lastDirection, Direction nextDirection, float t)
        {
            return (lastDirection, nextDirection) switch
            {
                (Direction.UP, Direction.UP) => new(-t, 0f, t, 0f),
                (Direction.UP, Direction.RIGHT) => new(-t, t, t, -t),
                (Direction.UP, Direction.LEFT) => new(-t, -t, t, t),
                (Direction.UP, Direction.NONE) => new(-t, 0f, t, 0f),

                (Direction.RIGHT, Direction.UP) => new(-t, t, t, -t),
                (Direction.RIGHT, Direction.RIGHT) => new(0f, t, 0f, -t),
                (Direction.RIGHT, Direction.DOWN) => new(t, t, -t, -t),
                (Direction.RIGHT, Direction.NONE) => new(0f, t, 0f, -t),

                (Direction.DOWN, Direction.RIGHT) => new(t, t, -t, -t),
                (Direction.DOWN, Direction.DOWN) => new(t, 0f, -t, 0f),
                (Direction.DOWN, Direction.LEFT) => new(t, -t, -t, t),
                (Direction.DOWN, Direction.NONE) => new(t, 0f, -t, 0f),

                (Direction.LEFT, Direction.UP) => new(-t, -t, t, t),
                (Direction.LEFT, Direction.DOWN) => new(t, -t, -t, t),
                (Direction.LEFT, Direction.LEFT) => new(0f, -t, 0f, t),
                (Direction.LEFT, Direction.NONE) => new(0f, -t, 0f, t),

                (Direction.NONE, Direction.NONE) => new(0f, 0f, 0f, 0f),

                _ => throw new ArgumentException("Error looking up vertex offset. Method: [VertexOffsetLookup()].")
            };
        }

        public struct Point : IDisposable
        {
            public float3 position;

            public NativeArray<Direction> directions;

            public Point(float3 position, int maxDirections, ref Unity.Mathematics.Random prng)
            {
                this.position = position;
                directions = new(maxDirections + 1, Allocator.TempJob);

                Direction lastDir = Direction.NONE;

                for (int i = 0; i < maxDirections; i++)
                {
                    directions[i] = RandomizeDirection(lastDir, ref prng);
                    lastDir = directions[i];
                }

                directions[maxDirections] = Direction.NONE;
            }

            public static Direction RandomizeDirection(Direction lastDirection, ref Unity.Mathematics.Random prng)
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

            public void Dispose()
            {
                if (directions.IsCreated)
                {
                    directions.Dispose();
                }
            }
        }

        [BurstCompile]
        private struct SegmentBuilder : IJob
        {
            [WriteOnly] public NativeArray<float3> vertices;

            [WriteOnly] public NativeArray<int> triangles;

            [WriteOnly] public NativeArray<float2> uvs;

            public NativeArray<Direction> directions;

            public float3 position;

            public float thickness;

            public void Execute()
            {
                float4 startOffsets = GetOriginOffsets(directions[0], thickness);

                vertices[0] = new float3(position.x + startOffsets.x, 0, position.z + startOffsets.y);
                vertices[1] = new float3(position.x + startOffsets.x, 5, position.z + startOffsets.y);
                vertices[2] = new float3(position.x + startOffsets.z, 5, position.z + startOffsets.w);
                vertices[3] = new float3(position.x + startOffsets.z, 0, position.z + startOffsets.w);

                SetQuadUVs(uvs, 0);
                SetTriangleQuad(0, 0, 1, 3, 2);

                int verts = 4, tris = 6;

                float4 prevOffsets = startOffsets;
                float3 prevPosition = position;

                for (int i = 0; i <= directions.Length - 2; i++)
                {
                    int distance = 5;

                    Direction lastDir = directions[i];
                    Direction nextDir = directions[i + 1];

                    float3 currentStartPos = prevPosition;
                    position += NormalizedDirections[(int)lastDir] * distance;
                    float3 currentEndPos = position;

                    float4 offsets = VertexOffsetTable(lastDir, nextDir, thickness);

                    vertices[verts] = new float3(currentEndPos.x + offsets.x, 0, currentEndPos.z + offsets.y);
                    vertices[verts + 1] = new float3(currentEndPos.x + offsets.x, 5, currentEndPos.z + offsets.y);
                    vertices[verts + 2] = new float3(currentStartPos.x + prevOffsets.x, 5, currentStartPos.z + prevOffsets.y);
                    vertices[verts + 3] = new float3(currentStartPos.x + prevOffsets.x, 0, currentStartPos.z + prevOffsets.y);
                    SetQuadUVs(uvs, verts);
                    SetTriangleQuad(tris, verts, verts + 1, verts + 3, verts + 2);

                    vertices[verts + 4] = new float3(currentStartPos.x + prevOffsets.z, 0, currentStartPos.z + prevOffsets.w);
                    vertices[verts + 5] = new float3(currentStartPos.x + prevOffsets.z, 5, currentStartPos.z + prevOffsets.w);
                    vertices[verts + 6] = new float3(currentEndPos.x + offsets.z, 5, currentEndPos.z + offsets.w);
                    vertices[verts + 7] = new float3(currentEndPos.x + offsets.z, 0, currentEndPos.z + offsets.w);
                    SetQuadUVs(uvs, verts + 4);
                    SetTriangleQuad(tris + 6, verts + 4, verts + 5, verts + 7, verts + 6);

                    prevOffsets = offsets;
                    prevPosition = currentEndPos;

                    verts += 8;
                    tris += 12;
                }

                vertices[verts] = new float3(prevPosition.x + prevOffsets.z, 0, prevPosition.z + prevOffsets.w);
                vertices[verts + 1] = new float3(prevPosition.x + prevOffsets.z, 5, prevPosition.z + prevOffsets.w);
                vertices[verts + 2] = new float3(prevPosition.x + prevOffsets.x, 5, prevPosition.z + prevOffsets.y);
                vertices[verts + 3] = new float3(prevPosition.x + prevOffsets.x, 0, prevPosition.z + prevOffsets.y);

                SetQuadUVs(uvs, verts);
                SetTriangleQuad(tris, verts, verts + 1, verts + 3, verts + 2);
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

            private readonly void SetQuadUVs(NativeArray<float2> targetUvs, int index)
            {
                targetUvs[index] = new float2(0f, 0f);
                targetUvs[index + 1] = new float2(0f, 1f);
                targetUvs[index + 2] = new float2(1f, 1f);
                targetUvs[index + 3] = new float2(1f, 0f);
            }

            private readonly float4 GetOriginOffsets(Direction direction, float t)
            {
                return direction switch
                {
                    Direction.UP => new(-t, -t, t, -t),
                    Direction.RIGHT => new(-t, t, -t, -t),
                    Direction.DOWN => new(t, t, -t, t),
                    Direction.LEFT => new(t, -t, t, t),
                    _ => new(0f, 0f, 0f, 0f)
                };
            }
        }

        [Button]
        public void GenerateSimpleSegment() 
        {
            seed = (uint)DateTime.Now.Ticks;
            Unity.Mathematics.Random prng = new(seed);

            int maxWalls = prng.NextInt(wallChainRange.x + 1, wallChainRange.y + 1);
            Point point = new(float3.zero, maxWalls, ref prng);

            int segmentCount = point.directions.Length - 1;
            int totalQuads = 1 + (segmentCount * 2) + 1;

            NativeArray<float3> vertsArr = new(totalQuads * 4, Allocator.TempJob);
            NativeArray<int> trisArr = new(totalQuads * 6, Allocator.TempJob);
            NativeArray<float2> uvsArr = new(totalQuads * 4, Allocator.TempJob);

            SegmentBuilder job = new()
            {
                vertices = vertsArr,
                triangles = trisArr,
                uvs = uvsArr,
                directions = point.directions,
                position = float3.zero,
                thickness = thickness,
            };

            job.Schedule().Complete();

            Vector3[] vertices = new Vector3[totalQuads * 4];
            int[] triangles = new int[totalQuads * 6];
            Vector2[] uvs = new Vector2[totalQuads * 4];

            vertsArr.Reinterpret<Vector3>().CopyTo(vertices);
            trisArr.CopyTo(triangles);
            uvsArr.Reinterpret<Vector2>().CopyTo(uvs);

            vertsArr.Dispose();
            trisArr.Dispose();
            uvsArr.Dispose();

            Mesh mesh = new()
            {
                vertices = vertices,
                triangles = triangles,
                uv = uvs
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

        [Button]
        public void GenerateMaze()
        {
            Chunk chunk = new(float2.zero, 50, 5, gray);
            //float2 chunkBL = new(chunk.position.x - (0.5f * chunk.length), chunk.position.y - (0.5f * chunk.length));
            float2 chunkBL = new(chunk.position.x, chunk.position.y);

            seed = (uint)DateTime.Now.Ticks;
            Unity.Mathematics.Random prng = new(seed);

            float spacing = (float)chunk.length / segments, chance = 0f;
            NativeList<float2> originPoints = new();

            for (int x = 0; x <= segments; x++)
            {
                for (int y = 0; y <= segments; y++)
                {
                    if ((x == 0 || x == segments || y == 0 || y == segments) && chance > pointSpawnChance.x)
                    {
                        chance -= pointSpawnChance.x;
                        continue;
                    }

                    chance += prng.NextFloat(pointSpawnChance.x, pointSpawnChance.y);

                    if (chance >= chanceThreshold)
                    {
                        float2 originPosition = new(chunkBL.x + (x * spacing), chunkBL.y + (y * spacing));
                        originPoints.Add(originPosition);

                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.transform.position = new Vector3(originPosition.x, 0, originPosition.y);
                        cube.transform.parent = chunk.transform;

                        chance = 0;
                    }
                }
            }
        }
    }
}
