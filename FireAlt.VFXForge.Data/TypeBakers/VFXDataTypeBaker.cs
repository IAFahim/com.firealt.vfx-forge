using System;

namespace FireAlt.VFXForge.Data
{
    public interface IVFXDataTypeBaker
    {
        object BakeBoxed();
    }
    
    [Serializable]
    public abstract class VFXDataTypeBaker<T> : IVFXDataTypeBaker
        where T : unmanaged
    {
        public abstract T Bake();
        object IVFXDataTypeBaker.BakeBoxed() => Bake();
    }

    [Serializable]
    public class DefaultVFXDataTypeBaker<T> : VFXDataTypeBaker<T> 
        where T : unmanaged
    {
        public T Data;

        public override T Bake() => Data;
    }
}