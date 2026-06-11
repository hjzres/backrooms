using NaughtyAttributes;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

        [SerializeField] [MinMaxSlider(1, 10)] private Vector2Int pointSpawnChance;

        [SerializeField] [MinMaxSlider(1, 20)] private Vector2Int wallChainRange;

        [SerializeField] [Min(0.1f)] private float thickness = 1f;

        [Header("Gizmo Settings")]

        [SerializeField] private bool drawGizmos;

        [SerializeField] [Min(0.1f)] private float gizmoRadius;

        [SerializeField] private Color gizmoColor;

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
            [NativeDisableUnsafePtrRestriction] public Point point;

            [WriteOnly] public NativeArray<float3> vertices;

            [WriteOnly] public NativeArray<int> triangles;

            public float thickness;

            public void Execute()
            {
                CreateOriginVerts(point, ref vertices);
                SetTriangleQuad(0, 0, 1, 3, 2);

                int verts = 4;
                int tris = 6;

                for (int i = 0; i <= point.directions.Length - 2; i++)
                {
                    int distance = 5;

                    Direction lastDir = point.directions[i];
                    Direction nextDir = point.directions[i + 1];

                    point.position += NormalizedDirections[(int)lastDir] * distance;

                    float4 offsets = VertexOffsetTable(lastDir, nextDir, thickness);

                    vertices[verts] = new float3(point.position.x + offsets.x, 0, point.position.z + offsets.y);
                    vertices[verts + 1] = new float3(point.position.x + offsets.x, 5, point.position.z + offsets.y);
                    vertices[verts + 2] = new float3(point.position.x + offsets.z, 5, point.position.z + offsets.w);
                    vertices[verts + 3] = new float3(point.position.x + offsets.z, 0, point.position.z + offsets.w);

                    SetTriangleQuad(tris, verts, verts + 1, verts - 4, verts - 3);
                    SetTriangleQuad(tris + 6, verts + 3, verts - 1, verts + 2, verts - 2);

                    verts += 4;
                    tris += 12;
                }

                SetTriangleQuad(tris, verts - 2, verts - 3, verts - 1, verts - 4);
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

            private readonly void CreateOriginVerts(Point point, ref NativeArray<float3> vertices)
            {
                Direction direction = point.directions[0];
                float x = point.position.x;
                float z = point.position.z;

                float minX = x - thickness, maxX = x + thickness;
                float minZ = z - thickness, maxZ = z + thickness;

                switch (direction)
                {
                    case Direction.UP:
                        vertices[0] = new float3(minX, 0, minZ);
                        vertices[1] = new float3(minX, 5, minZ);
                        vertices[2] = new float3(maxX, 5, minZ);
                        vertices[3] = new float3(maxX, 0, minZ);
                        break;

                    case Direction.RIGHT:
                        vertices[0] = new float3(minX, 0, maxZ); 
                        vertices[1] = new float3(minX, 5, maxZ);
                        vertices[2] = new float3(minX, 5, minZ); 
                        vertices[3] = new float3(minX, 0, minZ);
                        break;

                    case Direction.DOWN:
                        vertices[0] = new float3(maxX, 0, maxZ); 
                        vertices[1] = new float3(maxX, 5, maxZ);
                        vertices[2] = new float3(minX, 5, maxZ); 
                        vertices[3] = new float3(minX, 0, maxZ);
                        break;

                    case Direction.LEFT:
                        vertices[0] = new float3(maxX, 0, minZ); 
                        vertices[1] = new float3(maxX, 5, minZ);
                        vertices[2] = new float3(maxX, 5, maxZ); 
                        vertices[3] = new float3(maxX, 0, maxZ);
                        break;

                    default:
                        throw new ArgumentException("Error making origin vertices. Method: [CreateOriginVerts()].");
                }
            }
        }

        [Button]
        public void GenerateSimpleSegment() 
        {
            seed = (uint)DateTime.Now.Ticks;
            Unity.Mathematics.Random prng = new(seed);

            int maxWalls = prng.NextInt(3, 8);

            Point point = new(float3.zero, maxWalls, ref prng);

            NativeArray<float3> vertsArr = new(point.directions.Length * 4, Allocator.TempJob);
            NativeArray<int> trisArr = new(point.directions.Length * 12, Allocator.TempJob);

            SegmentBuilder job = new()
            {
                vertices = vertsArr,
                triangles = trisArr,
                thickness = thickness,
                point = point,
            };

            job.Schedule().Complete();

            Vector3[] vertices = new Vector3[point.directions.Length * 4];
            int[] triangles = new int[point.directions.Length * 12];

            vertsArr.Reinterpret<Vector3>().CopyTo(vertices);
            trisArr.CopyTo(triangles);

            vertsArr.Dispose();
            trisArr.Dispose();

            Mesh mesh = new()
            {
                vertices = vertices,
                triangles = triangles
            };

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            GameObject obj = new("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            obj.GetComponent<MeshFilter>().mesh = mesh;
            obj.GetComponent<MeshRenderer>().material = gray;
        }
    }
}
