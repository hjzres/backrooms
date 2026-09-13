using Unity.Mathematics;

namespace Assets.Scripts
{
    public static class RandomUtility
    {
        public static Random prng;

        private static bool isInitialized;

        public static void Initialize(uint seed = 1)
        {
            if (seed == 0)
            {
                throw new System.ArgumentException("Seed for PRNG cannot be 0!");
            }

            prng = new(seed);
            isInitialized = true;
        }

        public static ref Random State
        {
            get
            {
                if (!isInitialized)
                {
                    Initialize();
                }

                return ref prng;
            }
        }
    }    
}
