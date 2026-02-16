using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Assets.Scripts.Levels
{
    public class LobbyGenerator : MonoBehaviour
    {
        private void Update()
        {
            Chunk chunk = new(float2.zero, 1, 50);
        }

        [Button]
        private void Test()
        {
            Chunk chunk = new(float2.zero, 1, 50);
        }
    }
}
