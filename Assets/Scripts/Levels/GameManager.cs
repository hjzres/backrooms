using UnityEngine;

namespace Assets.Scripts.Levels
{
    public class GameManager : MonoBehaviour
    {
        public static int seed;

        public static System.Random prng;

        private void Awake()
        {
            prng = new System.Random();
        }
    }
}
