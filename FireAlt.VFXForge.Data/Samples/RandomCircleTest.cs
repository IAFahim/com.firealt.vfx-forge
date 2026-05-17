using System;
using KrasCore;
using Unity.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FireAlt.VFXForge.Data
{
    [Serializable]
    public class RandomCircleTest : VFXArrayDataTypeBaker<Vector3>
    {
        [Min(0)]
        public int count;
        public float radius;
        public uint seed = 0;
        
        public override NativeArray<Vector3> Bake()
        {
            var array = new NativeArray<Vector3>(count, Allocator.Temp);
            
            var rng = seed == 0 ? new Unity.Mathematics.Random((uint)Random.Range(0, int.MaxValue)) : new Unity.Mathematics.Random(seed);
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = rng.NextPointInUnitSphere() * radius;
            }
            return array;
        }
    }
}