using System;
using Unity.Mathematics;
using UnityEngine;
using NaughtyAttributes;

namespace Assets.Scripts.Levels
{
    public class Hub : MonoBehaviour
    {
        [SerializeField] private GameObject hubPrefab;

        [SerializeField] private float4 quadRange;
    }
}
