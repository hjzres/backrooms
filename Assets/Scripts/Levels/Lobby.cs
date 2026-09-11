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

        [Header("Level Materials")]

        [SerializeField] private Material gray;

        [SerializeField] private Material arrowWallpaper;

        [SerializeField] private Material carpet;

        [Header("Generation Parameters")]

        [SerializeField] [Min(0.1f)] private float vertexOffset = 1f;

        [SerializeField] [Min(1)] private int wallLength = 5;

        [SerializeField] [Min(1)] private int segments, chanceThreshold;

        [SerializeField] [MinMaxSlider(0f, 1f)] private Vector2 pointSpawnChance;

        [SerializeField] [MinMaxSlider(1, 20)] private Vector2Int wallChainRange;

        private static readonly float2[] NormalizedDirections = new float2[5] 
        { 
            new(0, 1),  // UP
            new(1, 0),  // RIGHT
            new(0, -1), // DOWN
            new(-1, 0), // LEFT
            float2.zero   // NONE
        };

        // Vertices are created in a clockwise direction starting with the bottom left and ending with the bottom right.
        // Offset values follow the order aforementioned. x is bottom left, y is top left, z is top right, w is bottom right.
        private static float4 VertexOffsetTable(Direction lastDirection, Direction nextDirection, float vertexOffset)
        {
            return (lastDirection, nextDirection) switch
            {
                (Direction.UP, Direction.UP) => new(-vertexOffset, 0f, vertexOffset, 0f),
                (Direction.UP, Direction.RIGHT) => new(-vertexOffset, vertexOffset, vertexOffset, -vertexOffset),
                (Direction.UP, Direction.LEFT) => new(-vertexOffset, -vertexOffset, vertexOffset, vertexOffset),
                (Direction.UP, Direction.NONE) => new(-vertexOffset, 0f, vertexOffset, 0f),

                (Direction.RIGHT, Direction.UP) => new(-vertexOffset, vertexOffset, vertexOffset, -vertexOffset),
                (Direction.RIGHT, Direction.RIGHT) => new(0f, vertexOffset, 0f, -vertexOffset),
                (Direction.RIGHT, Direction.DOWN) => new(vertexOffset, vertexOffset, -vertexOffset, -vertexOffset),
                (Direction.RIGHT, Direction.NONE) => new(0f, vertexOffset, 0f, -vertexOffset),

                (Direction.DOWN, Direction.RIGHT) => new(vertexOffset, vertexOffset, -vertexOffset, -vertexOffset),
                (Direction.DOWN, Direction.DOWN) => new(vertexOffset, 0f, -vertexOffset, 0f),
                (Direction.DOWN, Direction.LEFT) => new(vertexOffset, -vertexOffset, -vertexOffset, vertexOffset),
                (Direction.DOWN, Direction.NONE) => new(vertexOffset, 0f, -vertexOffset, 0f),

                (Direction.LEFT, Direction.UP) => new(-vertexOffset, -vertexOffset, vertexOffset, vertexOffset),
                (Direction.LEFT, Direction.DOWN) => new(vertexOffset, -vertexOffset, -vertexOffset, vertexOffset),
                (Direction.LEFT, Direction.LEFT) => new(0f, -vertexOffset, 0f, vertexOffset),
                (Direction.LEFT, Direction.NONE) => new(0f, -vertexOffset, 0f, vertexOffset),

                _ => float4.zero
            };
        }

        private struct Point
        {
            public float2 position;

            public int dirStartIndex;

            public int dirEndIndex;
        }

        [BurstCompile]
        private struct MazeBuilder : IJob
        {
            [Unity.Collections.ReadOnly] public NativeArray<Point> points;

            [Unity.Collections.ReadOnly] public NativeArray<Direction> directions;

            [WriteOnly] public NativeArray<float3> vertices;

            [WriteOnly] public NativeArray<int> triangles; 

            [WriteOnly] public NativeArray<float2> uvs;

            [Unity.Collections.ReadOnly] public float vertexOffset;

            [Unity.Collections.ReadOnly] public float wallLength;

            public void Execute()
            {
                int verts = 0;
                int tris = 0;

                for (int i = 0; i < points.Length; i++)
                {
                    Point point = points[i];

                    CreateOriginVerts(directions[point.dirStartIndex], point.position.x, point.position.y, verts, ref vertices);
                    SetQuadUVs(verts, ref uvs);
                    SetTriangleQuad(tris, verts, verts + 1, verts + 3, verts + 2);

                    verts += 4;
                    tris += 6;

                    for (int j = point.dirStartIndex; j < point.dirEndIndex - 1; j++)
                    {
                        Direction lastDir = directions[j];
                        Direction nextDir = directions[j + 1];

                        point.position += NormalizedDirections[(int)lastDir] * wallLength;

                        float4 offsets = VertexOffsetTable(lastDir, nextDir, vertexOffset);

                        vertices[verts] = new float3(point.position.x + offsets.x, 0, point.position.y + offsets.y);
                        vertices[verts + 1] = new float3(point.position.x + offsets.x, 5, point.position.y + offsets.y);
                        vertices[verts + 2] = new float3(point.position.x + offsets.z, 5, point.position.y + offsets.w);
                        vertices[verts + 3] = new float3(point.position.x + offsets.z, 0, point.position.y + offsets.w);

                        SetQuadUVs(verts, ref uvs);
                        SetTriangleQuad(tris, verts, verts + 1, verts - 4, verts - 3);
                        SetTriangleQuad(tris + 6, verts + 3, verts - 1, verts + 2, verts - 2);

                        verts += 4;
                        tris += 12;
                    }

                    SetTriangleQuad(tris, verts - 2, verts - 3, verts - 1, verts - 4);
                    tris += 6;
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

            private void SetQuadUVs(int index, ref NativeArray<float2> uvs)
            {
                uvs[index] = new float2(0f, 0f);
                uvs[index + 1] = new float2(0f, 1f);
                uvs[index + 2] = new float2(1f, 1f);
                uvs[index + 3] = new float2(1f, 0f);
            }

            // Used Gemini to make the method cleaner than the eyesore it was before, will make something myself sometime when I optimize.
            private readonly void CreateOriginVerts(Direction initialDir, float x, float z, int index, ref NativeArray<float3> vertices)
            {
                if (initialDir == Direction.NONE) return;

                // Lookup vectors that specify local (X, Z) offsets relative to direction orientation
                // Columns: [0] Bottom-Left, [1] Top-Left, [2] Top-Right, [3] Bottom-Right
                ReadOnlySpan<float2> cornerOffsets = (initialDir) switch
                {
                    Direction.UP    => stackalloc float2[4] { new(-1, -1), new(-1, -1), new(1, -1),  new(1, -1) },
                    Direction.RIGHT => stackalloc float2[4] { new(-1, 1),  new(-1, 1),  new(-1, -1), new(-1, -1) },
                    Direction.DOWN  => stackalloc float2[4] { new(1, 1),   new(1, 1),   new(-1, 1),  new(-1, 1) },
                    Direction.LEFT  => stackalloc float2[4] { new(1, -1),  new(1, -1),  new(1, 1),   new(1, 1) },

                    _ => stackalloc float2[4] { float2.zero, float2.zero, float2.zero, float2.zero }
                };

                // Assign the 4 vertices using heights 0, 5, 5, 0 (matching your original vertical arrangement)
                vertices[index] = math.float3(x + cornerOffsets[0].x * vertexOffset, 0, z + cornerOffsets[0].y * vertexOffset);
                vertices[index + 1] = math.float3(x + cornerOffsets[1].x * vertexOffset, 5, z + cornerOffsets[1].y * vertexOffset);
                vertices[index + 2] = math.float3(x + cornerOffsets[2].x * vertexOffset, 5, z + cornerOffsets[2].y * vertexOffset);
                vertices[index + 3] = math.float3(x + cornerOffsets[3].x * vertexOffset, 0, z + cornerOffsets[3].y * vertexOffset);
            }
        }

        [Button]
        public void GenerateMaze()
        {
            seed = (uint)DateTime.Now.Ticks;
            Unity.Mathematics.Random prng = new(seed);

            Chunk chunk = new(float2.zero, 50, 1, gray);
            NativeList<Point> points = new(0, Allocator.TempJob);
            NativeList<Direction> directions = new(0, Allocator.TempJob);

            DetermineStartCoordinates(chunk, ref points, ref prng);
            AssignDirections(ref points, ref directions, ref prng);
            CalculateMeshCounts(points, directions, out int verticeLength, out int triangleLength);

            NativeArray<float3> vertsArr = new(verticeLength, Allocator.TempJob);
            NativeArray<int> trisArr = new(triangleLength, Allocator.TempJob);
            NativeArray<float2> uvsArr = new(verticeLength, Allocator.TempJob);

            MazeBuilder job = new()
            {
                points = points,
                directions = directions,
                vertices = vertsArr,
                triangles = trisArr,
                uvs = uvsArr,
                vertexOffset = vertexOffset,
                wallLength = wallLength
            };

            job.Schedule().Complete();

            Vector3[] vertices = new Vector3[verticeLength];
            int[] triangles = new int[triangleLength];
            Vector2[] uvs = new Vector2[verticeLength];

            vertsArr.Reinterpret<Vector3>().CopyTo(vertices);
            trisArr.CopyTo(triangles);
            uvsArr.Reinterpret<Vector2>().CopyTo(uvs);

            points.Dispose();
            directions.Dispose();
            vertsArr.Dispose();
            trisArr.Dispose();
            uvsArr.Dispose();

            Mesh mesh = new() { vertices = vertices, triangles = triangles, uv = uvs };

            HandleShading(mesh);

            GameObject mazeObj = new("Maze", typeof(MeshFilter), typeof(MeshRenderer));
            mazeObj.GetComponent<MeshFilter>().mesh = mesh;
            mazeObj.GetComponent<MeshRenderer>().material = arrowWallpaper; 
            mazeObj.transform.parent = chunk.transform;
        }

        private void DetermineStartCoordinates(Chunk chunk, ref NativeList<Point> points, ref Unity.Mathematics.Random prng)
        {
            float spacing = (float)chunk.length / segments;
            float chance = 0f;

            for (int x = 0; x <= segments; x++)
            {
                for (int z = 0; z <= segments; z++)
                {
                    if (chance > pointSpawnChance.x && (x == 0 || x == segments || z == 0 || z == segments))
                    {
                        chance -= pointSpawnChance.x;
                        continue;
                    }

                    chance += prng.NextFloat(pointSpawnChance.x, pointSpawnChance.y);

                    if (chance >= chanceThreshold)
                    {
                        float2 coord = new(chunk.transform.position.x + x * spacing, chunk.transform.position.z + z * spacing);
                        Point point = new();
                        point.position = coord;

                        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        visual.transform.position = new float3(coord.x, 0, coord.y);
                        visual.transform.parent = chunk.transform; 
                        points.Add(point);

                        chance = 0f;
                    }
                }
            }
        }

        // Assigns all directions to one array that is split using dirStartIndex and dirEndIndex from the Point struct.
        // Every set of directions for the points ends with Direction.NONE
        private void AssignDirections(ref NativeList<Point> points, ref NativeList<Direction> directions, ref Unity.Mathematics.Random prng)
        {
            int startIndex = 0;

            for (int i = 0; i < points.Length; i++)
            {
                Point point = points[i];
                Direction lastDir = Direction.NONE;
                int amount = prng.NextInt(wallChainRange.x, wallChainRange.y);

                for (int j = 0; j <= amount; j++)
                {
                    Direction nextDir = RandomizeDirection(lastDir, ref prng);
                    directions.Add(nextDir);
                    lastDir = nextDir;
                }

                directions.Add(Direction.NONE);

                point.dirStartIndex = startIndex;
                point.dirEndIndex = directions.Length;
                startIndex = directions.Length;
                points[i] = point;
            }
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

        // Gemini assisted with this yet again because it was impossible to just simply calculate the counts,
        // I will come back in the future and optimize things. This will do for now.
        private static void CalculateMeshCounts(NativeList<Point> points, NativeList<Direction> directions, out int vertCount, out int triCount)
        {
            vertCount = 0;
            triCount = 0;

            for (int i = 0; i < points.Length; i++)
            {
                Point point = points[i];

                // Origin Quad
                vertCount += 4;
                triCount += 6;

                // Loop for segments in the wall chain
                int segmentsInChain = point.dirEndIndex - 1 - point.dirStartIndex;

                if (segmentsInChain > 0)
                {
                    vertCount += segmentsInChain * 4;
                    triCount += segmentsInChain * 12;
                }

                // End Quad
                triCount += 6;
            }
        }

        // This function was done by Gemini, lighting is a field I really
        // struggle in right now and it drove me nuts...
        private void HandleShading(Mesh mesh)
        {
            // 1. Split shared vertices to enforce hard edges
            Vector3[] splitVerts;
            Vector2[] splitUVs;
            int[] splitTris;
            SplitMeshVertices(mesh, out splitVerts, out splitUVs, out splitTris);

            mesh.Clear();
            mesh.vertices = splitVerts;
            mesh.uv = splitUVs;
            mesh.triangles = splitTris;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static void SplitMeshVertices(Mesh mesh, out Vector3[] newVerts, out Vector2[] newUVs, out int[] newTris)
        {
            Vector3[] oldVerts = mesh.vertices;
            Vector2[] oldUVs = mesh.uv;
            int[] oldTris = mesh.triangles;

            newVerts = new Vector3[oldTris.Length];
            newUVs = new Vector2[oldTris.Length];
            newTris = new int[oldTris.Length];

            for (int i = 0; i < oldTris.Length; i++)
            {
                int index = oldTris[i];
                newVerts[i] = oldVerts[index];
                newUVs[i] = oldUVs[index];
                newTris[i] = i;
            }
        }
    }
}
