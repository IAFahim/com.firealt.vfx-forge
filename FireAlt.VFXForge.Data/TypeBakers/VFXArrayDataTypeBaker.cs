using System;
using System.Collections.Generic;
using KrasCore;
using Unity.Collections;

namespace FireAlt.VFXForge.Data
{
    public interface IVFXArrayDataTypeBaker
    {
        NativeArray<byte> BakeBytes();
    }
    
    [Serializable]
    public abstract class VFXArrayDataTypeBaker<T> : IVFXArrayDataTypeBaker
        where T : unmanaged
    {
        public abstract NativeArray<T> Bake();
        NativeArray<byte> IVFXArrayDataTypeBaker.BakeBytes() => Bake().AsBytes();
    }
    
    [Serializable]
    public class DefaultVFXArrayDataTypeBaker<T> : VFXArrayDataTypeBaker<T> 
        where T : unmanaged
    {
        public List<T> Data = new();

        public override NativeArray<T> Bake()
        {
            return Data.ToNativeArray(Allocator.Temp);
        }
    }
}