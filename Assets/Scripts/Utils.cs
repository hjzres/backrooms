using System;

namespace Assets.Scripts
{
    public static class Utils
    {
        public static float NextFloat(float min, float max, System.Random prng)
        {
            if (min > max)
            {
                throw new ArgumentException("Variable 'min' must be smaller than variable 'max' [Method: NextFloat()].");
            }

            double range = max - min;

            return (float)(min + prng.NextDouble() * range);
        }
    }
}
