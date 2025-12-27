using UnityEngine;
using NaughtyAttributes;
using static Assets.Scripts.Chunks;

namespace Assets.Scripts
{
    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] [Min(1)] private int meshLength = 1;

        [SerializeField] [Min(1)] private int resolution = 1;

        [SerializeField] private Material defaultMaterial;

        [Button]
        public void Generate()
        {
            SquareChunk chunk = new SquareChunk(Vector2.zero, meshLength, resolution, defaultMaterial);
        }
    }
}
