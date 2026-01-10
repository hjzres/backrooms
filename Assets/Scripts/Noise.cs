using UnityEngine;

namespace Assets.Scripts
{
    public static class Noise
    {
        // Very random noise.
        public static float WhiteNoise2D(Vector2 value)
        {
            Vector2 sinValue = new Vector2(Mathf.Sin(value.x), Mathf.Sin(value.y));
            float rand = Frac(Mathf.Sin(Vector2.Dot(sinValue, new Vector2(12.9898f, 78.233f))) * 143758.5453f);

            return rand;
        }

        private static float Frac(float num)
        {
            return num - Mathf.Floor(num);
        }
    }
}
