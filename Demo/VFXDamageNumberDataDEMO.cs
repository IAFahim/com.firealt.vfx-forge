using System;
using FireAlt.VFXForge.Data;
using KrasCore;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

namespace FireAlt.VFXForge.Demo
{
    [Serializable]
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct VFXDamageNumberDataDEMO
    {
        public Vector3 Position;
        public uint Type;
    }
    
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct VFXDamageNumberArrayDataDEMO
    {
        public uint GlyphIndex;
    }
    
    [Serializable]
    public class DamageNumberGlyphBaker : VFXArrayDataTypeBaker<VFXDamageNumberArrayDataDEMO>
    {
        public int number;

        public override NativeArray<VFXDamageNumberArrayDataDEMO> Bake()
        {
            var list = new NativeList<VFXDamageNumberArrayDataDEMO>(Allocator.Temp);
            BakeGlyphs(number, list);
            return list.AsArray();
        }

        public static void BakeGlyphs(int number, NativeList<VFXDamageNumberArrayDataDEMO> dataList)
        {
            var num = math.abs(number);

            while (dataList.Length == 0 || num > 0)
            {
                var digit = num % 10;
                num /= 10;

                dataList.Add(new VFXDamageNumberArrayDataDEMO
                {
                    GlyphIndex = (uint)digit
                });
            }

            if (number < 0)
            {
                dataList.Add(new VFXDamageNumberArrayDataDEMO
                {
                    GlyphIndex = 10
                });
            }

            dataList.Reverse();
        }
    }
}