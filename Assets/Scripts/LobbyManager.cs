using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;
using static Assets.Scripts.Chunks;

namespace Assets.Scripts
{
    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] [Min(1)] private int meshLength = 1;

        [SerializeField] [Min(1)] private int resolution = 1;

        [SerializeField] private Material defaultMaterial;

        [SerializeField] private Transform player;

        [SerializeField] private static Dictionary<Vector3, SquareChunk> squareChunks;

        [Button]
        public void Generate()
        {
            SquareChunk chunk = new SquareChunk(Vector2.zero, meshLength, resolution);
            chunk.gameObject.GetComponent<MeshRenderer>().material = defaultMaterial;
        }

        private void Awake()
        {
            squareChunks = new Dictionary<Vector3, SquareChunk>();
        }

        private void LateUpdate()
        {
            UpdateClientChunks(squareChunks, player.position, meshLength, defaultMaterial);
        }
    }
}
