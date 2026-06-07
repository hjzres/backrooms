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
        public enum Direction // Relative to xz plane where +z is UP and +x is RIGHT
        {
            UP,
            RIGHT,
            DOWN,
            LEFT,
            NONE
        }
        
        private static readonly float3[] normalizedDirections = { new(0, 0, 1), new(1, 0, 0), new(0, 0, -1), new(-1, 0, 0) };

        [Min(1)]
        [SerializeField] private uint seed;

        [Header("Generation Settings")]

        [SerializeField] private Material gray;

        [Min(1)]
        [SerializeField] private int segments, chanceThreshold;

        [MinMaxSlider(1, 10)]
        [SerializeField] private Vector2Int pointSpawnChance;

        [MinMaxSlider(1, 20)]
        [SerializeField] private Vector2Int wallChainRange;

        [Header("Gizmo Settings")]

        [SerializeField] private bool drawGizmos;

        [Min(0.1f)]
        [SerializeField] private float gizmoRadius;

        [SerializeField] private Color gizmoColor;



        [SerializeField] private Direction testDirection;
        
        public struct Point
        {
            public float3 position;

            public Direction[] directions;

            public Direction nextDir;

            public Point(float3 position, int maxDirections)
            {
                this.position = position;
                directions = new Direction[maxDirections];
                nextDir = Direction.NONE;
            }

            public static Direction RandomizeDirection(Direction previousDirection, ref Unity.Mathematics.Random prng)
            {
                int num = prng.NextInt(0, 4);

                return num switch
                {
                    0 => previousDirection == Direction.DOWN ? Direction.DOWN : Direction.UP,
                    1 => previousDirection == Direction.LEFT ? Direction.LEFT : Direction.RIGHT,
                    2 => previousDirection == Direction.UP ? Direction.UP : Direction.DOWN,
                    3 => previousDirection == Direction.RIGHT ? Direction.RIGHT : Direction.LEFT,
                    _ => throw new ArgumentException("An error occured choosing a random direction for the next wall."),
                };
            }
        }

        [Button]
        public void GenerateSimpleSegment()
        {
            //seed = (uint)DateTime.Now.Ticks;
            Unity.Mathematics.Random prng = new(seed);

            int maxWalls = prng.NextInt(3, 8);

            Point point = new(float3.zero, maxWalls);

            Direction previousDir = Direction.NONE;

            List<Vector3> vertices = new();
            PlaceEndVertices(testDirection, point, ref vertices);

            List<int> triangles = new()
            {
                0, 1, 3, 1, 2, 3
            };

            for (int i = 0; i < maxWalls; i++) // UP, LEFT, UP, LEFT
            {
                //int distance = 5;

                //point.nextDir = Point.RandomizeDirection(previousDir, ref prng);
                //point.position += normalizedDirections[(int)point.nextDir] * distance;
                //previousDir = point.nextDir;

                point.directions[i] = Point.RandomizeDirection(previousDir, ref prng);
                previousDir = point.directions[i];
            }

            BuildSegments(point, ref prng);

            Mesh mesh = new()
            {
                vertices = vertices.ToArray(),
                triangles = triangles.ToArray()
            };

            mesh.RecalculateNormals();

            GameObject obj = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            obj.GetComponent<MeshFilter>().mesh = mesh;
            obj.GetComponent<MeshRenderer>().material = gray;
        }

        private void PlaceEndVertices(Direction direction, Point point, ref List<Vector3> vertices)
        {
            var (x, z) = (point.position.x, point.position.z);

            var offsets = direction switch
            {
                Direction.UP => new[] { (x - 1, z - 1), (x - 1, z - 1), (x + 1, z - 1), (x + 1, z - 1) },
                Direction.RIGHT => new[] { (x - 1, z + 1), (x - 1, z + 1), (x - 1, z - 1), (x - 1, z - 1) },
                Direction.DOWN => new[] { (x + 1, z + 1), (x + 1, z + 1), (x - 1, z + 1), (x - 1, z + 1) },
                Direction.LEFT => new[] { (x + 1, z - 1), (x + 1, z - 1), (x + 1, z + 1), (x + 1, z + 1) },

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

        private void BuildSegments(Point point, ref Unity.Mathematics.Random prng)
        {
            for (int i = 0; i < point.directions.Length; i++)
            {

            }
        }

        private struct SegmentBuilder : IJob
        {
            public void Execute()
            {
                throw new NotImplementedException();
            }
        }

        private void PlaceVertices() // DOT PRODUCT FOLK
        {

        }

        /*[Button]
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
            //int totalVertices = 0; // Gotta do some crazy cases to get this number.

            RandomizeStartPoints(chunk.length, ref totalWallsToGenerate, ref startPointsList, prng);
            //SetChainDirections(ref startPointsList, prng);

            DirectionRandomizer directionRandJob = new() 
            { 
                prng = prng,
                points = startPointsList.ToNativeArray(Allocator.TempJob)
            };

            JobHandle job = directionRandJob.Schedule(totalWallsToGenerate, 4);
            job.Complete();

            for (int i = 0; i < totalWallsToGenerate; i++)
            {
                print(startPointsList[i]);
            }

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
                        probability--;
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
            //for (int i = 0; i < pointsList.Count; i++)
            //{
            //    Direction previousDir = Direction.NONE;

            //    for (int j = 0; j < pointsList[i].totalWalls; j++)
            //    {
            //        pointsList[i].precalculatedDirections[j] = Point.RandomizeDirection(previousDir, ref prng);
            //        previousDir = pointsList[i].precalculatedDirections[j];
            //    }
            //}
        }

        [BurstCompile]
        public struct DirectionRandomizer : IJobParallelFor
        {
            public Unity.Mathematics.Random prng;

            public NativeArray<Point> points; 

            public void Execute(int index)
            {
                Direction previousDir = Direction.NONE;

                Point point = points[index];

                for (int i = 0; i < points[index].totalWalls; i++)
                {
                    point.precalculatedDirections[i] = RandomizeDirection(previousDir, ref prng);
                    previousDir = point.precalculatedDirections[i];
                }

                points[index] = point;
            }

            public readonly Direction RandomizeDirection(Direction previousDir, ref Unity.Mathematics.Random prng)
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
        }*/

        //private void OnDrawGizmos()
        //{
        //    if (drawGizmos)
        //    {
        //        Gizmos.color = gizmoColor;

        //        if (gizmoStartPoints == null) return;

        //        for (int i =  0; i < gizmoStartPoints.Count; i++)
        //        {
        //            Gizmos.DrawWireSphere(gizmoStartPoints[i].position, gizmoRadius);
        //        }
        //    }
        //}

        //[Button]
        //public void ClearGizmoStartPoints()
        //{
        //    if (gizmoStartPoints != null)
        //    {
        //        gizmoStartPoints.Clear();
        //        DestroyImmediate(GameObject.Find("Chunk"));
        //        return;
        //    }

        //    throw new Exception("List is null, cannot clear.");
        //}
    }
}
