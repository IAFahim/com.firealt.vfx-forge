using Unity.Entities;

namespace FireAlt.VFXForge.Data
{
    public struct RegisteredVFX : ICleanupComponentData
    {
        public VFXKey Key;
    }
}