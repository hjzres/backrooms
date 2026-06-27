// using NaughtyAttributes;
// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using Unity.Burst;
// using Unity.Collections;
// using Unity.Jobs;
// using Unity.Mathematics;
// using UnityEngine;
// using UnityEngine.Jobs;
// using static UnityEngine.Rendering.PostProcessing.HistogramMonitor;

// namespace Assets.Scripts.Levels
// {
//     public enum NextDirection
//     {
//         NONE, UP, DOWN, LEFT, RIGHT
//     }

//     public class LobbyGenerator : MonoBehaviour
//     {
//         [SerializeField] private Transform player;

//         [Header("Materials")]

//         [SerializeField] private Material grayMaterial;

//         [Header("Rendering Properties")]
//         // DATA TO BE SENT TO UI SCRIPTS
//         [SerializeField][Min(1)] private uint seed = 1;

//         [SerializeField][Min(1)] private int renderDistance = 1;

//         [SerializeField] private int maxQueuedChunks = 4;

//         private Dictionary<Vector2Int, Chunk> chunkDictionary;

//         private Vector2Int lastPlayerCoords;

//         private Queue<Chunk> chunkQueue;

//         private GameObject chunkContainer;

//         private static Unity.Mathematics.Random prng;

//         [Header("Generator Configurations")]

//         [SerializeField][Min(1)] private float height; 

//         [SerializeField] private int segments;

//         [SerializeField] private Vector2 pointSpawnChance;

//         [SerializeField] private float chanceThreshold;

//         [SerializeField] private bool showSpawnPoints = false;

//         private struct Point
//         {
//             public Vector3 nextPosition;

//             public NextDirection nextDirection;

//             public readonly NextDirection RandomizeDirection()
//             {
//                 switch (prng.NextInt(1, sizeof(NextDirection)))
//                 {
//                     case 1:
//                         return NextDirection.UP;

//                     case 2:
//                         return NextDirection.DOWN;

//                     case 3:
//                         return NextDirection.LEFT;

//                     case 4:
//                         return NextDirection.RIGHT;

//                     default:
//                         break;
//                 }

//                 return NextDirection.NONE;
//             }
//         }

//         private void Awake()
//         {
//             chunkDictionary = new Dictionary<Vector2Int, Chunk>();
//             lastPlayerCoords = new Vector2Int(100, 100); // Arbitrary initial value.
//             chunkQueue = new Queue<Chunk>();

//             chunkContainer = new GameObject("Chunk Container");
//             prng = new Unity.Mathematics.Random(seed);
//         }

//         private void LateUpdate()
//         {
//             Vector2Int playerCoords = ComputeChunkCoords(new Vector2(player.position.x, player.position.z), 100);

//             if (lastPlayerCoords != playerCoords)
//             {
//                 for (int x = -renderDistance; x <= renderDistance; x++)
//                 {
//                     for (int y = -renderDistance; y <= renderDistance; y++)
//                     {
//                         Vector2Int currentCoord = new(playerCoords.x + x, playerCoords.y + y);

//                         if (!chunkDictionary.ContainsKey(currentCoord))
//                         {
//                             Vector2 position = currentCoord * 100;
//                             Chunk chunk = new(position, 1, 100, grayMaterial, chunkContainer.transform, chunk => { GenerateLobby(chunk, currentCoord); } );
//                             chunkDictionary.Add(currentCoord, chunk);
//                         }
//                     }
//                 }

//                 // HEAVILY UNPERFORMANT AT HIGH CHUNK COUNTS
//                 foreach (var chunk in chunkDictionary)
//                 {
//                     Vector2Int delta = chunk.Key - playerCoords;
//                     bool isWithinRange = Mathf.Abs(delta.x) <= renderDistance && Mathf.Abs(delta.y) <= renderDistance;
//                     chunk.Value.gameObject.SetActive(isWithinRange);
//                 }

//                 lastPlayerCoords = playerCoords;
//             }
//         }

//         private static Vector2Int ComputeChunkCoords(Vector2 positionXZ, int chunkSize) => new(Mathf.RoundToInt(positionXZ.x / chunkSize), Mathf.RoundToInt(positionXZ.y / chunkSize));

//         private void GenerateLobby(Chunk chunk, Vector2 coord)
//         {
//             //float noise = Noise.WhiteNoise2D(new Vector2(coord.x + seed, coord.y + seed)) * 100;

//             List<Point> startPoints = new();

//             RandomizeStartingPoints(chunk.position, startPoints, chunk.transform);

//         }

//         private void RandomizeStartingPoints(Vector2 origin, List<Point> startPoints, Transform visualParent)
//         {
//             float spacing = 100f / segments;
//             float chance = 0f;

//             for (int x = 0; x <= segments; x++)
//             {
//                 for (int y = 0; y <= segments; y++)
//                 {
//                     if ((x == 0 || x == segments || y == 0 || y == segments) && chance > pointSpawnChance.x)
//                     {
//                         chance -= pointSpawnChance.x;
//                         continue;
//                     }

//                     chance += prng.NextFloat(pointSpawnChance.x, pointSpawnChance.y);

//                     if (chance >= chanceThreshold)
//                     {
//                         Point point = new() { nextPosition = new Vector3(origin.x + x * spacing, height * 0.5f, origin.y + y * spacing) };
                        
//                         if (showSpawnPoints)
//                         {
//                             GameObject pointVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
//                             pointVisual.transform.position = point.nextPosition;
//                             pointVisual.transform.SetParent(visualParent);
//                         }

//                         startPoints.Add(point);
//                         chance = 0f;
//                     }
//                 }
//             }
//         }

//         private void DestroyObject(GameObject gameObject)
//         {
//             if (Application.isPlaying)
//             {
//                 Destroy(gameObject);
//             }

//             else
//             {
//                 DestroyImmediate(gameObject);
//             }
//         }

//         [Button]
//         public void GenerateTestChunk()
//         {
//             prng = new Unity.Mathematics.Random(seed);

//             Chunk chunk = new(Vector2.zero, 1, 100, grayMaterial, null, chunk => { GenerateLobby(chunk, Vector2.zero); });
//         }
//     }
// }
