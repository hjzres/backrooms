using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts
{
    public class Chunks
    {
        public static int clientDistanceInChunks = 3;

        public static float distanceThreshold = 100f;

        public class SquareChunk
        {
            public Vector2 position;

            public Vector3[] vertices;

            public Vector3[] normals;

            public Vector2[] uvs;

            public int[] triangles;

            public int resolution;

            public int length;

            public GameObject gameObject;

            private readonly Action<SquareChunk> onCreate;

            public SquareChunk(Vector2 position, int length, int resolution, Action<SquareChunk> onCreate)
            {
                Mesh mesh = new Mesh();
                GameObject gameObject = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                
                // Offset position to center chunk.
                gameObject.transform.position = new Vector3(position.x - length * 0.5f, 0, position.y - length * 0.5f);

                Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
                Vector3[] normals = new Vector3[(resolution + 1) * (resolution + 1)];
                Vector2[] uvs = new Vector2[(resolution + 1) * (resolution + 1)];
                int[] triangles = new int[6 * resolution * resolution]; // Derived by the Drei

                this.position = position;
                this.resolution = resolution;
                this.length = length;

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

                this.onCreate = onCreate;
                onCreate?.Invoke(this);
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
    }
}
