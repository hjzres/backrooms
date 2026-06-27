using Unity.Mathematics;
using UnityEngine;

namespace Assets.Scripts
{
    public class Chunk
    {
        public GameObject gameObject;

        public Transform transform;

        public int length;

        public int resolution;

        public float2 position;

        private readonly Vector3[] vertices, normals;

        private readonly int[] triangles;

        private readonly Vector2[] uvs;

        public Chunk(float2 position, int length, int resolution, Material material, Transform parent = null)
        {
            gameObject = new("Chunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            transform = gameObject.transform;
            transform.position = new Vector3(position.x, 0, position.y);

            this.position = position;
            this.length = length;
            this.resolution = resolution;

            vertices = new Vector3[(resolution + 1) * (resolution + 1)];
            triangles = new int[6 * resolution * resolution]; // Derived by the Drei (Big D)
            normals = new Vector3[(resolution + 1) * (resolution + 1)];
            uvs = new Vector2[(resolution + 1) * (resolution + 1)];

            CreateMeshData();

            Mesh mesh = new()
            {
                vertices = vertices,
                triangles = triangles,
                normals = normals,
                uv = uvs  
            };

            mesh.RecalculateNormals();
            gameObject.GetComponent<MeshFilter>().mesh = mesh;
            gameObject.GetComponent<MeshRenderer>().material = material;
            gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;
            transform.SetParent(parent);
        }

        private void CreateMeshData()
        {
            float spacing = (float)length / resolution;
            int verts = 0, tris = 0;

            for (int i = 0; i <= resolution; i++)
            {
                for (int j = 0; j <= resolution; j++)
                {
                    int index = i * (resolution + 1) + j;

                    vertices[index] = new(i * spacing, 0, j * spacing);
                    normals[index] = new(0, 0, -1);
                    uvs[index] = new(vertices[index].x, vertices[index].z);

                    if (i != resolution && j != resolution)
                    {
                        triangles[tris] = verts + 1;
                        triangles[tris + 1] = verts + resolution + 2;
                        triangles[tris + 2] = verts;
                        triangles[tris + 3] = verts;
                        triangles[tris + 4] = verts + resolution + 2;
                        triangles[tris + 5] = verts + resolution + 1;

                        tris += 6;
                        verts++;
                    }
                }

                if (i != resolution)
                {
                    verts++;   
                }
            }
        }
    }
}
