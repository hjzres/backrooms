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
    public enum Direction // Relative to xz plane where +z is UP and +x is RIGHT
    {
        UP,
        RIGHT,
        DOWN,
        LEFT,
        NONE
    }

    public class Lobby : MonoBehaviour
    {
        [Min(1)] public uint seed;

        [Header("Generation Settings")]

        public Material gray;

        [Min(1)] public int segments;

        [Min(1)] public int chanceThreshold;

        [MinMaxSlider(1, 10)] public Vector2Int pointSpawnChance;

        [MinMaxSlider(1, 20)] public Vector2Int wallChainRange;

        [Header("Gizmo Settings")]

        public bool drawGizmos;

        [Min(0.1f)] public float gizmoRadius;

        public Color gizmoColor;

        private List<Point> gizmoStartPoints;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------ //

        private static readonly float3[] NormalizedDirections = 
        { 
            new(0, 0, 1), 
            new(1, 0, 0), 
            new(0, 0, -1), 
            new(-1, 0, 0) 
        };

        public struct Point
        {
            public float3 position;

            public Direction nextPointDir;

            public int totalWalls;

            public Direction[] precalculatedDirections;

            public Point(float3 position, int totalWalls = 0)
            {
                this.position = position;
                this.nextPointDir = Direction.NONE;
                this.totalWalls = totalWalls;
                this.precalculatedDirections = new Direction[totalWalls];
            }

            public static Direction RandomizeDirection(Direction previousDir, ref Unity.Mathematics.Random prng)
            {
                int num = prng.NextInt(0, 4);

                return num switch
                {
                    0 => previousDir == Direction.DOWN ? Direction.DOWN : Direction.UP,
                    1 => previousDir == Direction.LEFT ? Direction.LEFT : Direction.RIGHT,
                    2 => previousDir == Direction.UP ? Direction.UP : Direction.DOWN,
                    3 => previousDir == Direction.RIGHT ? Direction.RIGHT : Direction.LEFT,
                    _ => throw new ArgumentException("An error occured choosing a random direction for the next wall."),
                };
            }
        }

        [Button]
        public void GenerateMaze()
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();

            if (drawGizmos)
            {
                gizmoStartPoints = new List<Point>();
            }

            Unity.Mathematics.Random prng = new(seed);

            Chunk chunk = new(Vector2.zero, 1, 50, gray);
            List<Point> startPointsList = new();
            int totalWallsToGenerate = 0;
            int totalVertices = 0; // Gotta do some crazy cases to get this number.

            RandomizeStartPoints(chunk.length, ref totalWallsToGenerate, ref startPointsList, prng);
            SetChainDirections(ref startPointsList, prng);

            stopwatch.Stop();
            print($"Time taken: {stopwatch.ElapsedMilliseconds} ms.");

            //NativeArray<Point> startPoints = new(startPointsList.ToArray(), Allocator.TempJob);
        }

        private void RandomizeStartPoints(int chunkLength, ref int counter, ref List<Point> list, Unity.Mathematics.Random prng)
        {
            float spacing = (float)chunkLength / segments;
            int probability = 0;

            for (int x = 0; x <= segments; x++)
            {
                for (int y = 0; y <= segments; y++)
                {
                    if ((x == 0 || x == segments || y == 0 || y == segments) && probability > pointSpawnChance.x)
                    {
                        probability -= 1;
                        continue;
                    }

                    probability += prng.NextInt(pointSpawnChance.x, pointSpawnChance.y + 1);

                    if (probability >= chanceThreshold)
                    {
                        Point point = new(new float3(x * spacing, 0, y * spacing), UnityEngine.Random.Range(wallChainRange.x, wallChainRange.y + 1));
                        counter += point.totalWalls;
                        list.Add(point);

                        probability = 0;

                        if (drawGizmos && gizmoStartPoints != null)
                        {
                            gizmoStartPoints.Add(point);
                        }
                    }
                }
            }
        }

        private void SetChainDirections(ref List<Point> pointsList, Unity.Mathematics.Random prng)
        {
            for (int i = 0; i < pointsList.Count; i++)
            {
                Direction previousDir = Direction.NONE;

                for (int j = 0; j < pointsList[i].totalWalls; j++)
                {
                    pointsList[i].precalculatedDirections[j] = Point.RandomizeDirection(previousDir, ref prng);
                    previousDir = pointsList[i].precalculatedDirections[j];
                }
            }
        }

        public struct MazeMeshJob : IJob
        {
            public NativeArray<Point> startPoints;

            public NativeArray<float3> vertices;

            public NativeArray<int> triangles;

            public void Execute()
            {
                for (int i = 0; i < startPoints.Length; i++)
                {
                    Direction previousDir = Direction.NONE;
                    Point point = new(float3.zero);

                    for (int j = 0; j < startPoints[i].totalWalls; j++)
                    {
                        
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (drawGizmos)
            {
                Gizmos.color = gizmoColor;

                if (gizmoStartPoints == null) return;

                for (int i =  0; i < gizmoStartPoints.Count; i++)
                {
                    Gizmos.DrawWireSphere(gizmoStartPoints[i].position, gizmoRadius);
                }
            }
        }

        [Button]
        public void ClearGizmoStartPoints()
        {
            if (gizmoStartPoints != null)
            {
                gizmoStartPoints.Clear();
                DestroyImmediate(GameObject.Find("Chunk"));
                return;
            }

            throw new Exception("List is null, cannot clear.");
        }
    }
}
