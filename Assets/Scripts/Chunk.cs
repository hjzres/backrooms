using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.Scripts
{
    public class Chunk
    {
        public GameObject gameObject;

        public Transform transform;

        public int resolution;

    //     public GameObject gameObject;

    //     public Transform transform;

    //     public int resolution;

    //     public int length;

    //     public Vector2 position;

    //     public Vector3[] vertices;

    //     private readonly Vector3[] normals;

    //     private readonly Vector2[] uvs;

    //     private readonly int[] tris;

    //     private readonly Action<Chunk> onCreate;

    //     public Chunk(Vector2 position, int resolution, int length, Material material, Transform parent = null, Action<Chunk> onCreate = null)
    //     {
    //         gameObject = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
    //         transform = gameObject.transform;
    //         transform.position = new Vector3(position.x, 0, position.y);
    //         this.position = position;
    //         this.resolution = resolution;
    //         this.length = length;
    //         this.onCreate = onCreate;

    //         tris = new int[6 * resolution * resolution]; // Derived by the Drei
    //         resolution++;

    //         int resSquared = resolution * resolution;
    //         vertices = new Vector3[resSquared];
    //         normals = new Vector3[resSquared];
    //         uvs = new Vector2[resSquared];

    //         SendAndReadJobData(resSquared);

    //         Mesh mesh = new()
    //         {
    //             vertices = vertices,
    //             normals = normals,
    //             uv = uvs,
    //             triangles = tris
    //         };

    //         mesh.RecalculateNormals();

    //         gameObject.GetComponent<MeshFilter>().mesh = mesh;
    //         gameObject.GetComponent<MeshRenderer>().material = material;
    //         gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;
    //         gameObject.isStatic = true;
    //         transform.SetParent(parent);

    //         onCreate?.Invoke(this);
    //     }

    //     private void SendAndReadJobData(int arraySize)
    //     {
    //         Allocator allocator = Application.isPlaying ? Allocator.TempJob : Allocator.Persistent;

    //         NativeArray<float3> vertsArray = new(arraySize, allocator);
    //         NativeArray<float3> normalsArray = new(arraySize, allocator);
    //         NativeArray<float2> uvsArray = new(arraySize, allocator);
    //         NativeArray<int> trisArray = new(6 * resolution * resolution, allocator);

    //         ChunkBuilderJob job = new()
    //         {
    //             _vertices = vertsArray,
    //             _normals = normalsArray,
    //             _uvs = uvsArray,
    //             _triangles = trisArray,
    //             _resolution = resolution,
    //             _length = length
    //         };

    //         JobHandle handle = job.Schedule();
    //         handle.Complete();

    //         for (int i = 0; i < arraySize; i++)
    //         {
    //             vertices[i] = vertsArray[i];
    //             normals[i] = normalsArray[i];
    //             uvs[i] = uvsArray[i];
    //         }

    //         for (int i = 0; i < trisArray.Length; i++)
    //         {
    //             tris[i] = trisArray[i];
    //         }

    //         vertsArray.Dispose();
    //         normalsArray.Dispose();
    //         uvsArray.Dispose();
    //         trisArray.Dispose();
    //     }

    //     public bool Flag() => false;
    // }

    // [BurstCompile]
    // public struct ChunkBuilderJob : IJob
    // {
    //     public NativeArray<float3> _vertices;

    //     public NativeArray<float3> _normals;

    //     public NativeArray<float2> _uvs;

    //     public NativeArray<int> _triangles;

    //     public int _resolution;

    //     public int _length;

    //     public void Execute()
    //     {
    //         float spacingBetweenVerts = (float)_length / _resolution;

    //         for (int i = 0; i <= _resolution; i++)
    //         {
    //             for (int j = 0; j <= _resolution; j++)
    //             {
    //                 float2 spacing = new(i * spacingBetweenVerts, j * spacingBetweenVerts);

    //                 _vertices[i * (_resolution + 1) + j] = new float3(spacing.x, 0, spacing.y);
    //                 _normals[i * (_resolution + 1) + j] = new float3(0, 0, -1);
    //                 _uvs[0] = new float2((float)i / _resolution, (float)j / _resolution);
    //             }
    //         }

    //         int vert = 0;
    //         int tris = 0;

    //         for (int x = 0; x < _resolution; x++)
    //         {
    //             for (int y = 0; y < _resolution; y++)
    //             {
    //                 _triangles[tris] = vert + 1;
    //                 _triangles[tris + 1] = vert + _resolution + 2;
    //                 _triangles[tris + 2] = vert + 0;
    //                 _triangles[tris + 3] = vert + 0;
    //                 _triangles[tris + 4] = vert + _resolution + 2;
    //                 _triangles[tris + 5] = vert + _resolution + 1;

    //                 tris += 6;
    //                 vert++;
    //             }

    //             vert++;
    //         }
    //     }
    }
}
