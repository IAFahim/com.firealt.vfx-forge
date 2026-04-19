using Unity.Entities;

namespace FireAlt.VFXForge.Data
{
    public struct CreateVFXData : IComponentData, IEnableableComponent
    {
        public VFXKey Key;
    }
}