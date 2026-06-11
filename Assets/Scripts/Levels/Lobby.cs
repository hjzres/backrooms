using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
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
        // Offset values follow the order aforementioned. [0] is bottom left, [1] is top left, [2] is top right, [3] is bottom right.
        private static float[] VertexOffsetTable(Direction lastDirection, Direction nextDirection, float thickness)
        {
            return (lastDirection, nextDirection) switch
            {
                (Direction.UP, Direction.UP) => new[] { -thickness, 0f, thickness, 0f },
                (Direction.UP, Direction.RIGHT) => new[] { -thickness, thickness, thickness, -thickness },
                (Direction.UP, Direction.LEFT) => new[] { -thickness, -thickness, thickness, thickness },
                (Direction.UP, Direction.NONE) => new[] { -thickness, 0f, thickness, 0f },

                (Direction.RIGHT, Direction.UP) => new[] { -thickness, thickness, thickness, -thickness },
                (Direction.RIGHT, Direction.RIGHT) => new[] { 0f, thickness, 0f, -thickness },
                (Direction.RIGHT, Direction.DOWN) => new[] { thickness, thickness, -thickness, -thickness },
                (Direction.RIGHT, Direction.NONE) => new[] { 0f, thickness, 0f, -thickness },

                (Direction.DOWN, Direction.RIGHT) => new[] { thickness, thickness, -thickness, -thickness },
                (Direction.DOWN, Direction.DOWN) => new[] { thickness, 0f, -thickness, 0f },
                (Direction.DOWN, Direction.LEFT) => new[] { thickness, -thickness, -thickness, thickness },
                (Direction.DOWN, Direction.NONE) => new[] { thickness, 0f, -thickness, 0f },

                (Direction.LEFT, Direction.UP) => new[] { -thickness, -thickness, thickness, thickness },
                (Direction.LEFT, Direction.DOWN) => new[] { thickness, -thickness, -thickness, thickness },
                (Direction.LEFT, Direction.LEFT) => new[] { 0f, -thickness, 0f, thickness },
                (Direction.LEFT, Direction.NONE) => new[] { 0f, -thickness, 0f, thickness },

                _ => throw new ArgumentException("Error looking up vertex offset. Method: [VertexOffsetLookup()].")
            };
        }

        public struct Point
        {
            public float3 position;

            public Direction nextDir;

            public NativeArray<Direction> directions;

            public Point(float3 position, int maxDirections, ref Unity.Mathematics.Random prng)
            {
                this.position = position;
                directions = new(maxDirections + 2, Allocator.TempJob);
                nextDir = Direction.NONE;

                Direction lastDir = Direction.NONE;

                for (int i = 0; i < maxDirections; i++)
                {
                    if (i == maxDirections + 1 || i == maxDirections + 2)
                    {
                        directions[i] = Direction.NONE;
                    }

                    directions[i] = RandomizeDirection(lastDir, ref prng);
                    lastDir = directions[i];
                }
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
        }

        private struct SegmentBuilder : IJob
        {
            public NativeArray<float3> vertices;

            public NativeArray<int> triangles;

            public float thickness;

            public Point point;

            public int maxWalls;

            public void Execute()
            {
                CreateOriginVerts(point, ref vertices);

                triangles[0] = 0;
                triangles[1] = 1;
                triangles[2] = 3;
                triangles[3] = 1;
                triangles[4] = 2;
                triangles[5] = 3;

                int verts = 4;
                int tris = 6;

                for (int i = 0; i <= maxWalls; i++)
                {
                    int distance = 5;

                    Direction lastDir = point.directions[i];
                    Direction nextDir = point.directions[i + 1];

                    point.position += NormalizedDirections[(int)lastDir] * distance;

                    float[] offsets = VertexOffsetTable(lastDir, nextDir, thickness);

                    vertices[verts] = new float3(point.position.x + offsets[0], 0, point.position.z + offsets[1]);
                    vertices[verts + 1] = new float3(point.position.x + offsets[0], 5, point.position.z + offsets[1]);
                    vertices[verts + 2] = new float3(point.position.x + offsets[2], 5, point.position.z + offsets[3]);
                    vertices[verts + 3] = new float3(point.position.x + offsets[2], 0, point.position.z + offsets[3]);

                    triangles[tris] = verts;
                    triangles[tris + 1] = verts + 1;
                    triangles[tris + 2] = verts - 4;

                    triangles[tris + 3] = verts + 1;
                    triangles[tris + 4] = verts - 3;
                    triangles[tris + 5] = verts - 4;

                    triangles[tris + 6] = verts - 1;
                    triangles[tris + 7] = verts - 2;
                    triangles[tris + 8] = verts + 2;

                    triangles[tris + 9] = verts - 1;
                    triangles[tris + 10] = verts + 2;
                    triangles[tris + 11] = verts + 3;

                    verts += 4;
                    tris += 12;
                }

                triangles[triangles.Length - 6] = verts - 2;
                triangles[triangles.Length - 5] = verts - 3;
                triangles[triangles.Length - 4] = verts - 1;
                triangles[triangles.Length - 3] = verts - 4;
                triangles[triangles.Length - 2] = verts - 1;
                triangles[triangles.Length - 1] = verts - 3;
            }

            private readonly void CreateOriginVerts(Point point, ref NativeArray<float3> vertices)
            {
                Direction direction = point.directions[0];
                var (x, z) = (point.position.x, point.position.z);

                var offsets = direction switch
                {
                    Direction.UP => new[] { (x - thickness, z - thickness), (x - thickness, z - thickness), (x + thickness, z - thickness), (x + thickness, z - thickness) },
                    Direction.RIGHT => new[] { (x - thickness, z + thickness), (x - thickness, z + thickness), (x - thickness, z - thickness), (x - thickness, z - thickness) },
                    Direction.DOWN => new[] { (x + thickness, z + thickness), (x + thickness, z + thickness), (x - thickness, z + thickness), (x - thickness, z + thickness) },
                    Direction.LEFT => new[] { (x + thickness, z - thickness), (x + thickness, z - thickness), (x + thickness, z + thickness), (x + thickness, z + thickness) },

                    _ => throw new ArgumentException($"Direction not supported: {direction}")
                };

                vertices[0] = new Vector3(offsets[0].Item1, 0, offsets[0].Item2);
                vertices[1] = new Vector3(offsets[1].Item1, 5, offsets[1].Item2);
                vertices[2] = new Vector3(offsets[2].Item1, 5, offsets[2].Item2);
                vertices[3] = new Vector3(offsets[3].Item1, 0, offsets[3].Item2);
            }
        }

        [Button]
        public void GenerateSimpleSegment() 
        {
            seed = (uint)DateTime.Now.Ticks;
            Unity.Mathematics.Random prng = new(seed);

            int maxWalls = prng.NextInt(3, 8);

            Point point = new(float3.zero, maxWalls, ref prng);

            Vector3[] vertices = new Vector3[point.directions.Length * 4];
            int[] triangles = new int[(point.directions.Length * 12)];

            NativeArray<float3> vertsArr = new(vertices.Length, Allocator.TempJob);
            NativeArray<int> trisArr = new(triangles.Length, Allocator.TempJob);

            //List <Vector3> vertices = new();
            //PlaceStarterVertices(point.directions[0], point, ref vertices);

            //List<int> triangles = new() { 0, 1, 3, 1, 2, 3 };

            //int verts = 4;

            //for (int i = 0; i < maxWalls + 1; i++)
            //{
            //    int distance = 5;

            //    Direction lastDir = point.directions[i];
            //    point.position += NormalizedDirections[(int)lastDir] * distance;

            //    Direction nextDir = point.directions[i + 1];

            //    float[] offsets = VertexOffsetTable(lastDir, nextDir, thickness);

            //    vertices.AddRange(new List<Vector3>
            //    {
            //        new(point.position.x + offsets[0], 0, point.position.z + offsets[1]),
            //        new(point.position.x + offsets[0], 5, point.position.z + offsets[1]),
            //        new(point.position.x + offsets[2], 5, point.position.z + offsets[3]),
            //        new(point.position.x + offsets[2], 0, point.position.z + offsets[3])
            //    });

            //    triangles.AddRange(new List<int>
            //    {
            //        verts, verts + 1, verts - 4,
            //        verts + 1, verts - 3, verts - 4,
            //        verts - 1, verts - 2, verts + 2,
            //        verts - 1, verts + 2, verts + 3
            //    });

            //    verts += 4;
            //}

            //triangles.AddRange(new List<int> { verts - 2, verts - 3, verts - 1, verts - 4, verts - 1, verts - 3 });

            SegmentBuilder job = new()
            {
                vertices = vertsArr,
                triangles = trisArr,
                thickness = thickness,
                point = point,
                maxWalls = maxWalls
            };

            JobHandle handle = job.Schedule();
            handle.Complete();

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = vertsArr[i];
            }

            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = trisArr[i];
            }

            vertsArr.Dispose();
            trisArr.Dispose();

            Mesh mesh = new()
            {
                vertices = vertices,
                triangles = triangles
            };

            mesh.RecalculateNormals();

            GameObject obj = new("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            obj.GetComponent<MeshFilter>().mesh = mesh;
            obj.GetComponent<MeshRenderer>().material = gray;
        }
    }
}
