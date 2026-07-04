using UnityEngine;

namespace Session
{
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [SerializeField] float baseIntensity = 1.4f;
        [SerializeField] float flickerAmount = 0.08f;
        [SerializeField] float flickerSpeed = 14f;
        [SerializeField] float dropChancePerSecond = 0.3f;
        [SerializeField] float dropDuration = 0.07f;

        Light cachedLight;
        float dropTimer;
        float noiseOffset;

        void Awake()
        {
            cachedLight = GetComponent<Light>();
            noiseOffset = Random.value * 100f;
        }

        void Update()
        {
            if (dropTimer > 0f)
            {
                dropTimer -= Time.deltaTime;
                cachedLight.intensity = baseIntensity * 0.45f;
                return;
            }

            if (Random.value < dropChancePerSecond * Time.deltaTime)
                dropTimer = dropDuration * Random.Range(0.5f, 2f);

            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed + noiseOffset, 0.37f) - 0.5f;
            cachedLight.intensity = baseIntensity + noise * 2f * flickerAmount;
        }
    }
}
