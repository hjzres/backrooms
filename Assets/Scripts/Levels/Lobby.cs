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

            public readonly Direction[] directions;

            public Point(float3 position, int maxDirections, ref Unity.Mathematics.Random prng)
            {
                this.position = position;
                directions = new Direction[maxDirections + 2];
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

        [Button]
        public void GenerateSimpleSegment() 
        {
            seed = (uint)DateTime.Now.Ticks;
            Unity.Mathematics.Random prng = new(seed);

            int maxWalls = prng.NextInt(3, 8);

            Point point = new(float3.zero, maxWalls, ref prng);

            List <Vector3> vertices = new();
            PlaceStarterVertices(point.directions[0], point, ref vertices);

            List<int> triangles = new() { 0, 1, 3, 1, 2, 3 };

            int verts = 4;

            for (int i = 0; i < maxWalls + 1; i++)
            {
                int distance = 5;

                Direction lastDir = point.directions[i];
                point.position += NormalizedDirections[(int)lastDir] * distance;

                Direction nextDir = point.directions[i + 1];

                float[] offsets = VertexOffsetTable(lastDir, nextDir, thickness);

                vertices.AddRange(new List<Vector3>
                {
                    new(point.position.x + offsets[0], 0, point.position.z + offsets[1]),
                    new(point.position.x + offsets[0], 5, point.position.z + offsets[1]),
                    new(point.position.x + offsets[2], 5, point.position.z + offsets[3]),
                    new(point.position.x + offsets[2], 0, point.position.z + offsets[3])
                });

                triangles.AddRange(new List<int>
                {
                    verts, verts + 1, verts - 4,
                    verts + 1, verts - 3, verts - 4,
                    verts - 1, verts - 2, verts + 2,
                    verts - 1, verts + 2, verts + 3
                });

                verts += 4;
            }

            triangles.AddRange(new List<int> { verts - 2, verts - 3, verts - 1, verts - 4, verts - 1, verts - 3 });

            Mesh mesh = new()
            {
                vertices = vertices.ToArray(),
                triangles = triangles.ToArray()
            };

            mesh.RecalculateNormals();

            GameObject obj = new("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            obj.GetComponent<MeshFilter>().mesh = mesh;
            obj.GetComponent<MeshRenderer>().material = gray;
        }

        private void PlaceStarterVertices(Direction direction, Point point, ref List<Vector3> vertices)
        {
            var (x, z) = (point.position.x, point.position.z);

            var offsets = direction switch
            {
                Direction.UP => new[] { (x - thickness, z - thickness), (x - thickness, z - thickness), (x + thickness, z - thickness), (x + thickness, z - thickness) },
                Direction.RIGHT => new[] { (x - thickness, z + thickness), (x - thickness, z + thickness), (x - thickness, z - thickness), (x - thickness, z - thickness) },
                Direction.DOWN => new[] { (x + thickness, z + thickness), (x + thickness, z + thickness), (x - thickness, z + thickness), (x - thickness, z + thickness) },
                Direction.LEFT => new[] { (x + thickness, z - thickness), (x + thickness, z - thickness), (x + thickness, z + thickness), (x + thickness, z + thickness) },

                _ => throw new ArgumentException($"Direction not supported: {direction}")
            };

            vertices = new()
            {
                new Vector3(offsets[0].Item1, 0, offsets[0].Item2),
                new Vector3(offsets[1].Item1, 5, offsets[1].Item2),
                new Vector3(offsets[2].Item1, 5, offsets[2].Item2),
                new Vector3(offsets[3].Item1, 0, offsets[3].Item2),
            };
        }

        private struct SegmentBuilder : IJob
        {
            public void Execute()
            {
                throw new NotImplementedException();
            }
        }
    }
}
