using UnityEngine;
using System.Collections.Generic;

namespace Assets.Scripts
{
    public class Chunks
    {
        public static int clientDistanceInChunks = 3;

        public class SquareChunk
        {
            public Vector2 position;

            public Vector3[] vertices;

            public Vector3[] normals;

            public Vector2[] uvs;

            public int[] triangles;

            public int resolution;

            public GameObject gameObject;

            public SquareChunk(Vector2 position, int length, int resolution)
            {
                Mesh mesh = new Mesh();
                GameObject gameObject = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                
                // Offset position to center chunk relative to (0, 0, 0).
                gameObject.transform.position = new Vector3(position.x - length * 0.5f, 0, position.y - length * 0.5f);

                Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
                Vector3[] normals = new Vector3[(resolution + 1) * (resolution + 1)];
                Vector2[] uvs = new Vector2[(resolution + 1) * (resolution + 1)];
                int[] triangles = new int[6 * resolution * resolution]; // Derived by the Drei

                this.position = position;
                this.resolution = resolution;

                this.vertices = vertices;
                this.normals = normals;
                this.uvs = uvs;
                this.triangles = triangles;
                this.gameObject = gameObject;

                float spacing = (float)length / resolution;

                GenerateNonTriangularConstituents(spacing);
                PlaceTriangles();
                
                mesh.vertices = vertices;
                mesh.uv = uvs;
                mesh.triangles = triangles;
                mesh.normals = normals;

                gameObject.GetComponent<MeshFilter>().mesh = mesh;
            }

            private void GenerateNonTriangularConstituents(float spacingBetweenVertices)
            {
                for (int i = 0; i < resolution + 1; i++)
                {
                    for (int j = 0; j < resolution + 1; j++)
                    {
                        float spacingX = i * spacingBetweenVertices;
                        float spacingY = j * spacingBetweenVertices;

                        vertices[i * (resolution + 1) + j] = new Vector3(spacingX, 0, spacingY);
                        uvs[i * (resolution + 1) + j] = new Vector2(spacingX, spacingY);
                        normals[i * (resolution + 1) + j] = Vector3.back;
                    }
                }
            }

            private void PlaceTriangles()
            {
                int vert = 0;
                int tris = 0;

                for (int x = 0; x < resolution; x++)
                {
                    for (int y = 0; y < resolution; y++)
                    {
                        triangles[tris] = vert + 1;
                        triangles[tris + 1] = vert + resolution + 2;
                        triangles[tris + 2] = vert + 0;
                        triangles[tris + 3] = vert + 0;
                        triangles[tris + 4] = vert + resolution + 2;
                        triangles[tris + 5] = vert + resolution + 1;

                        tris += 6;
                        vert++;
                    }

                    vert++;
                }
            }
        }

        public static void UpdateClientChunks(Dictionary<Vector3, SquareChunk> squareChunks, Vector3 clientPosition, int chunkLength, Material chunkMaterial)
        {
            Vector2 clientChunkCoord = ComputeCliendChunkCoords(clientPosition, chunkLength);

            for (int x = -clientDistanceInChunks; x <= clientDistanceInChunks; x++)
            {
                for (int y = -clientDistanceInChunks; y <= clientDistanceInChunks; y++)
                {
                    Vector2 coordinates = new Vector2(clientChunkCoord.x + x, clientChunkCoord.y + y);

                    if (!squareChunks.ContainsKey(new Vector3(coordinates.x, 0, coordinates.y)))
                    {
                        Vector2 chunkPosition = coordinates * chunkLength;
                        SquareChunk chunk = new SquareChunk(chunkPosition, chunkLength, 1);
                        chunk.gameObject.GetComponent<MeshRenderer>().material = chunkMaterial;

                        squareChunks.Add(new Vector3(coordinates.x, 0, coordinates.y), chunk);
                    }
                }
            }
        }

        private static Vector2 ComputeCliendChunkCoords(Vector3 clientPosition, int chunkLength)
        {
            float operationX = clientPosition.x / (chunkLength * 0.5f);
            float operationY = clientPosition.z / (chunkLength * 0.5f);

            if (clientPosition.x < 0)
            {
                return new Vector2(Mathf.CeilToInt(operationX), Mathf.CeilToInt(operationY));
            }

            return new Vector2(Mathf.FloorToInt(operationX), Mathf.FloorToInt(operationY));
        }
    }
}
